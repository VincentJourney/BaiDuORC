using Core.FrameWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaiDuOCR.Model;
using BaiDuOCR.Model.Util;
using BaiDuOCR.FrameWork;
using System.Reflection;
using BaiDuOCR.Core;
using BaiDuOCR.Model.Entity;
using Newtonsoft.Json;
using System.Threading;
using System.Net;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Web;
using RabbitMQ.Client;

namespace BaiDuOCR.Core
{
    public static class OCRVerify
    {
        private static OCRDAL dal = new OCRDAL();

        private static CacheHelper cacheHelper = new CacheHelper();


        /// <summary>
        /// 图片识别
        /// </summary>
        /// <param name="cardId"></param>
        /// <param name="mallId"></param>
        /// <param name="base64"></param>
        /// <returns>识别结果</returns>
        public static async Task<Result> ReceiptOCR(string mallId, string base64)
        {

            var result = await Task.Run(() =>
            {
                var OcrResult = BaiDuReceiptOCR(mallId, base64);
                if (OcrResult.Success)
                    return RecognitOCRResult(OcrResult.Data.ToString());
                else
                    return OcrResult;
            });
            return new Result(false, "", result);

        }

        /// <summary>
        /// 调用百度OCR小票识别服务 返回原始数据
        /// </summary>
        /// <param name="mallId"></param>
        /// <param name="base64"></param>
        /// <returns></returns>
        private static Result BaiDuReceiptOCR(string mallId, string base64)
        {
            try
            {
                base64 = System.Web.HttpUtility.UrlEncode(base64);
                MallOCRRule MallRule = CacheHandle<MallOCRRule>($"MallOCRRule{mallId}", 1, $" and MallID='{mallId}'");
                var result = HttpHelper.HttpPost(string.Format(MallRule.OCRServerURL, MallRule.OCRServerToken), $"image={HttpUtility.UrlEncode(base64)}");
                if (result.Contains("error_msg"))
                {
                    var OCRErrorModel = JsonConvert.DeserializeObject<OCRErrorResult>(result);
                    return new Result(false, OCRErrorModel.error_msg, null);
                }
                else
                {
                    return new Result(true, "", result);
                }
            }
            catch (Exception ex)
            {
                Log.Error("BaiDuReceiptOCR", ex);
                return new Result(false, ex.Message, null);
            }
        }


        /// <summary>
        /// 从OCR接口中 根据规则 获取详细内容 （暂无校验）
        /// </summary>
        /// <param name="OcrResult"></param>
        /// <returns></returns>
        private static Result RecognitOCRResult(string OcrResult)
        {
            try
            {
                var OCRResultModel = JsonConvert.DeserializeObject<OCRResult>(OcrResult);
                var WordList = OCRResultModel.words_result;//被识别的内容
                var OCRStoreName = string.Empty;//被识别出来的商铺名称
                                                //查询所有的商铺规则并缓存
                List<StoreOCRDetail> AllStoreOCRDetailRuleList = CacheHandle<List<StoreOCRDetail>>(Key: "AllStoreOCRDetailRuleLists", Hour: 24, sqlWhere: "");
                //所有商铺名称规则
                var StoreNameRuleList = AllStoreOCRDetailRuleList.Where(s => s.OCRKeyType == (int)OCRKeyType.StoreNo).Select(c => c.OCRKey).ToList();

                var resultStoreName = FindStoreNameFromAllRule(WordList, StoreNameRuleList);//根据所有的店铺名规则匹配出的StoreName

                var StoreModel = dal.GetModel<Store>($" and StoreName like '%{resultStoreName}%'");

                if (StoreModel == null)
                    return new Result(false, "识别商铺名称失败！", "");

                //最后识别结果
                var ReceiptOCRModel = new ReceiptOCR
                {
                    StoreId = StoreModel.StoreId,
                    StoreName = resultStoreName,
                    StoreCode = StoreModel.StoreCode,
                    ReceiptNo = "",
                    TranAmount = 0,
                    TransDatetime = Commom.DefaultDateTime,
                    RecongnizelId = Guid.Empty,
                    Base64 = ""
                };

                //当前店铺的规则明细
                var ThisStoreOCRDetail = AllStoreOCRDetailRuleList.Where(s => s.StoreId == StoreModel.StoreId).ToList();

                //根据店铺规则明细 关键字类型 关键字 取值方法 匹配识别结果
                foreach (var StoreDetailRule in ThisStoreOCRDetail)
                {
                    for (int i = 0; i < WordList.Count(); i++)
                    {
                        Result ReturnResult = GetValue(WordList, i, StoreDetailRule); //根据规则取值
                        if (ReturnResult.Success)
                        {
                            var ReturnData = ReturnResult.Data.ToString();
                            switch (StoreDetailRule.OCRKeyType) //枚举有注释，根据关键字类型赋值
                            {
                                case (int)OCRKeyType.ReceiptNO:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && string.IsNullOrWhiteSpace(ReceiptOCRModel.ReceiptNo))
                                    {
                                        ReceiptOCRModel.ReceiptNo = ReturnResult.Data.ToString();
                                        continue;
                                    }
                                    break;
                                case (int)OCRKeyType.DateTime:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && ReceiptOCRModel.TransDatetime == Commom.DefaultDateTime)
                                    {
                                        ReturnData = ReturnData.Replace(" ", "").Insert(10, " ");//可能会识别成 2019 - 05 - 1512:14:44 转datetime 报错
                                        if (DateTime.TryParse(ReturnData, out var DateTimeResult))
                                        {
                                            ReceiptOCRModel.TransDatetime = DateTimeResult;
                                            continue;
                                        }
                                    }
                                    break;
                                case (int)OCRKeyType.Amount:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && ReceiptOCRModel.TranAmount == 0)
                                    {
                                        if (decimal.TryParse(ReturnResult.Data.ToString(), out var AmountResult))
                                        {
                                            ReceiptOCRModel.TranAmount = AmountResult;
                                            continue;
                                        }
                                    }
                                    break;
                                default:
                                    return new Result(false, $"商铺未设置该关键字类型取值方法：{StoreDetailRule.OCRKeyType}", null);
                            }
                        }
                    }
                }

                var RecongnizeModelId = Guid.NewGuid();
                var RecongnizeModel = new ApplyPictureRecongnize
                {
                    id = RecongnizeModelId,
                    applyid = Guid.NewGuid(),
                    Lineno = 0,
                    LineContent = OcrResult,
                    OCRResult = JsonConvert.SerializeObject(ReceiptOCRModel)
                };
                var AddResult = DbContext.Add(RecongnizeModel);
                //添加成功后 出参RecongnizeModelId
                if (AddResult == 0)
                    ReceiptOCRModel.RecongnizelId = RecongnizeModelId;
                else
                    return new Result(false, "ApplyPictureRecongnize Fail", null);

                return new Result(true, "识别成功", ReceiptOCRModel);
            }
            catch (Exception ex)
            {
                Log.Error("VerifyOCRResult", ex);
                return new Result(false, ex.Message, null);
            }

            //同步寻找
            string FindStoreNameFromAllRule(List<Words> words, List<string> StoreNameList)
            {
                foreach (var Name in StoreNameList)
                {
                    foreach (var word in words)
                    {
                        if (word.words.Contains(Name))
                            return word.words;
                    }
                }
                return "";
            }

            //异步寻找
            async Task<string> FindStoreNameFromAllRuleAsync(List<Words> words, List<string> StoreNameList)
            {
                var Sresult = "";
                foreach (var names in StoreNameList)
                {
                    var LineCount = words.Count();//识别内容数量
                                                  //二分异步寻找
                    var afterLine = LineCount % 2 == 1 ? (LineCount - 1) / 2 : LineCount / 2;//前一半
                    var beforeLine = LineCount - afterLine;//后一半
                    CancellationTokenSource ct = new CancellationTokenSource();
                    for (int i = 0; i < 2; i++)
                    {
                        var min = i % 2 == 0 ? 0 : afterLine;
                        var max = i % 2 == 0 ? afterLine - 1 : LineCount;
                        await Task.Run(() =>
                           {
                               for (int j = min; j < max; j++)
                               {
                                   if (ct.IsCancellationRequested)
                                   {
                                       if (words[j].words.Contains(names))
                                       {
                                           ct.Cancel();
                                           Sresult = words[j].words;
                                       }
                                   }
                               }
                           }, ct.Token);
                    }
                }
                return "";
            }

            //根据规则获取指定识别内容
            Result GetValue(List<Words> words_result, int index, StoreOCRDetail StoreDetailRule)
            {
                var WordValue = "";
                if (words_result[index].words.Contains(StoreDetailRule.OCRKey))
                {
                    switch (StoreDetailRule.GetValueWay)//可查看枚举注释
                    {
                        case (int)GetValueWay.OCRKey:
                            WordValue = words_result[index].words;
                            break;
                        case (int)GetValueWay.AfterOCRKey:
                            var IndexOfKey = words_result[index].words.IndexOf(StoreDetailRule.OCRKey) + StoreDetailRule.OCRKey.Length + StoreDetailRule.SkipLines;
                            WordValue = words_result[index].words.Substring(IndexOfKey);
                            break;
                        case (int)GetValueWay.NextLine:
                            if (index + StoreDetailRule.SkipLines <= words_result.Count())
                                WordValue = words_result[index + StoreDetailRule.SkipLines].words;
                            break;
                        default:
                            return new Result(false, $"商铺未设置该关键字取值方法：{StoreDetailRule.GetValueWay}", null);
                    }
                }
                return new Result(true, "", WordValue);
            }
        }


        /// <summary>
        /// 根据信息校验用户是否篡改信息，是否满足商铺积分规则
        /// </summary>
        /// <param name="receiptOCR"></param>
        /// <returns></returns>
        private static Result VerifyRecognition(ReceiptOCR receiptOCR)
        {
            //识别的原始数据
            var RecongnizeModel = dal.GetModel<ApplyPictureRecongnize>($" and id ='{receiptOCR.RecongnizelId}'");
            if (RecongnizeModel == null)
                return new Result(false, "ApplyPictureRecongnize is null", null);

            #region 检查数据是否篡改(Contain 不是绝对)
            ReceiptOCR OldReceipt = JsonConvert.DeserializeObject<ReceiptOCR>(RecongnizeModel.OCRResult);
            var CompareResult = CompareModel<ReceiptOCR>(OldReceipt, receiptOCR);
            if (!CompareResult.Success)
                return CompareResult;
            #endregion

            #region 匹配商铺规则
            StoreOCR StoreOCRRule = CacheHandle<StoreOCR>($"StoreOCR{receiptOCR.StoreId}", 0.5, $"and StoreId = '{receiptOCR.StoreId}'");

            if (StoreOCRRule.Enabled != 1)
                return new Result(false, "商铺未启用自动积分", null);
            if (StoreOCRRule.needVerify == 1) //当商铺启用校验规则
            {
                if (receiptOCR.TranAmount < StoreOCRRule.MinValidReceiptValue || receiptOCR.TranAmount > StoreOCRRule.MaxValidReceiptValue)
                    return new Result(false, "小票金额不在店铺规则范围之内！", null);

                if (StoreOCRRule.MaxTicketPerDay != 0)  //日自动交易笔数为0时 代表不限制
                {
                    var TicketPerDay = DbContext.Query<int>($@"select count(*) from ApplyPoint
                               where StoreID = '{receiptOCR.StoreId}' and VerifyStatus = 1 
                               and SourceType=7 and DATEDIFF(dd,AuditDate,GETDATE())=0").FirstOrDefault(); //当日交易笔数
                    if (TicketPerDay >= StoreOCRRule.MaxTicketPerDay)
                        return new Result(false, "今日已超过最大自动积分记录数量", null);
                }

                Store StoreModel = CacheHandle<Store>($"Store{receiptOCR.StoreId}", 1, $" and StoreId = '{receiptOCR.StoreId}'");

                if ((StoreModel.IsStandardPOS == "1" ? 0 : 1) != StoreOCRRule.POSType)
                    return new Result(false, "OCR商铺POS类型不一致", null);

                //POS门店代码暂无验证
            }
            #endregion

            return new Result(true, "验证成功", null);
        }

        /// <summary>
        /// 创建积分申请单，校验信息成功并推送
        /// </summary>
        /// <param name="cardId"></param>
        /// <param name="receiptOCR"></param>
        /// <returns></returns>
        public static async Task<Result> CreateApplyPoint(string cardId, ReceiptOCR receiptOCR)
        {
            try
            {
                Store StoreModel = CacheHandle<Store>($"Store{receiptOCR.StoreId}", 1, $" and StoreId = '{receiptOCR.StoreId}'");
                StoreOCR StoreOCRRule = CacheHandle<StoreOCR>($"StoreOCR{receiptOCR.StoreId}", 0.5, $"and StoreId = '{receiptOCR.StoreId}'");
                var ApplyPoint = dal.GetModel<ApplyPoint>($" and ReceiptNo='{receiptOCR.ReceiptNo}' and StoreID='{receiptOCR.StoreId}'");
                var IsHas = false;// 是否原先存在积分申请单  默认没有
                if (ApplyPoint == null)  //判断该小票号 是否存在积分申请单 不存在则添加原始积分申请单 （已解析原始数据，校验失败）
                {
                    var ApplyPointId = Guid.NewGuid();
                    ApplyPoint = new ApplyPoint
                    {
                        ApplyPointID = ApplyPointId,
                        MallID = StoreModel.MallID,
                        StoreID = receiptOCR.StoreId,
                        CardID = Guid.Parse(cardId),
                        ReceiptNo = await FileUpload(Commom.FileUploadUrl, receiptOCR.ReceiptNo),
                        TransDate = receiptOCR.TransDatetime,
                        TransAmt = receiptOCR.TranAmount,
                        VerifyStatus = StoreOCRRule.needVerify == 0 ? 1 : 0
                    };
                }
                else
                {
                    IsHas = true;
                    ApplyPoint.MallID = StoreModel.MallID;
                    ApplyPoint.StoreID = receiptOCR.StoreId;
                    ApplyPoint.CardID = Guid.Parse(cardId);
                    ApplyPoint.ReceiptNo = receiptOCR.ReceiptNo;
                    ApplyPoint.TransDate = receiptOCR.TransDatetime;
                    ApplyPoint.TransAmt = receiptOCR.TranAmount;
                    ApplyPoint.VerifyStatus = StoreOCRRule.needVerify == 0 ? 1 : 0;
                }

                var VerifyRecognitionResult = VerifyRecognition(receiptOCR);//校验
                ApplyPoint.AuditDate = DateTime.Now;
                if (VerifyRecognitionResult.Success)
                {
                    ApplyPoint.RecongizeStatus = 2;
                    ApplyPoint.VerifyStatus = 1;
                }
                else
                {
                    //校验失败 修改值
                    ApplyPoint.RecongizeStatus = 3;
                    ApplyPoint.VerifyStatus = 0;
                    if (IsHas)
                        DbContext.Update<ApplyPoint>(ApplyPoint);
                    else
                        DbContext.Add<ApplyPoint>(ApplyPoint);
                    return VerifyRecognitionResult;
                }

                if (IsHas)
                    DbContext.Update<ApplyPoint>(ApplyPoint);
                else
                    DbContext.Add<ApplyPoint>(ApplyPoint);

                //自动积分 推送
                var arg = new WebPosArg
                {
                    companyID = StoreModel.CompanyID.ToString(),
                    storeID = StoreModel.StoreId.ToString(),
                    cardID = dal.GetModel<Card>($" and CardID='{cardId}'")?.CardCode ?? "",
                    cashierID = "CrmApplyPoint",
                    discountPercentage = 0,
                    orgID = StoreModel.OrgID.ToString(),
                    receiptNo = receiptOCR.ReceiptNo,
                    txnDateTime = receiptOCR.TransDatetime
                };
                var posResult = await WebPosForPoint(arg);

                return posResult;
            }
            catch (Exception ex)
            {
                Log.Error("CreateApplyPoint", ex);
                return new Result(false, ex.Message, null);
            }
        }


        /// <summary>
        /// 自动积分并微信推送接口
        /// </summary>
        /// <param name="webPosArg"></param>
        /// <returns></returns>
        public async static Task<Result> WebPosForPoint(WebPosArg webPosArg)
        {
            int amount = new Random(((unchecked((int)DateTime.Now.Millisecond + (int)DateTime.Now.Ticks)))).Next(10, 2000);
            string testString = $@"<cmd type=""SALES"" appCode=""POS"">
                                        <shared offline=""true"">
                                                <companyID>{webPosArg.companyID}</companyID>
                                                <orgID>{webPosArg.orgID}</orgID>
                                                <storeID>{webPosArg.storeID}</storeID>
                                                <cashierID>{webPosArg.cashierID}</cashierID>
                                        </shared>
                                        <sales discountPercentage=""{webPosArg.discountPercentage}"" 
                                        cardID=""{webPosArg.cardID}"" 
                                        txnDateTime=""{webPosArg.txnDateTime}"" 
                                        receiptNo=""12L102N10120180321uytu7t876896{amount}"" 
                                        actualAmount=""{amount}"" 
                                        payableAmount=""{amount}"" 
                                        verificationCode="""" />
                                   </cmd>";
            WebPosTest.WebPOSSoapClient ws = new WebPosTest.WebPOSSoapClient(WebPosTest.WebPOSSoapClient.EndpointConfiguration.WebPOSSoap);
            WebPosTest.WebPOSCredentials mwc = new WebPosTest.WebPOSCredentials();


            mwc.Username = ConfigurationUtil.GetSection("ObjectConfig:WebPosService:UserName");
            mwc.Password = ws.GetEncryptedCharAsync(ConfigurationUtil.GetSection("ObjectConfig:WebPosService:PassWord")).Result;
            try
            {
                var cmdResult = await ws.CmdAsync(mwc, testString);
                if (!string.IsNullOrWhiteSpace(cmdResult.CmdResult) && cmdResult.CmdResult.Contains("hasError=\"false\""))
                {
                    return new Result(true, "OCR识别成功，完成自动积分且进行微信推送", null);
                }
                return new Result(false, $"OCR验证通过，{cmdResult.CmdResult}", null);
            }
            catch (Exception ex)
            {
                Log.Error("WebPos接口", ex);
                return new Result(false, $"OCR验证通过，{ex.Message}", null);

            }
        }



        /// <summary>
        /// 对比同一类型Model的数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="oldModel"></param>
        /// <param name="newModel"></param>
        /// <returns></returns>
        private static Result CompareModel<T>(T oldModel, T newModel) where T : class
        {
            var oldInfo = oldModel.GetType().GetProperties(BindingFlags.Public);
            var newInfo = newModel.GetType().GetProperties(BindingFlags.Public);
            foreach (var item in oldInfo)
            {
                var newinfoObj = newInfo.Where(s => s.Name == item.Name).FirstOrDefault();
                if (newinfoObj != null || newinfoObj.GetValue(newModel, null).ToString().Contains(item.GetValue(oldModel, null).ToString()))
                    return new Result(false, "OCR数据与提交数据不一致", null);
            }
            return new Result(true, "", null);

        }

        /// <summary>
        /// 若有缓存则从缓存取，如果缓存中没有则查询并放入缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Key">缓存键</param>
        /// <param name="Hour">缓存有效时长</param>
        /// <param name="sqlWhere"></param>
        /// <returns></returns>
        private static T CacheHandle<T>(string Key, double Hour, string sqlWhere) where T : class
        {
            try
            {
                T Value = default;
                if (cacheHelper.Exists(Key))
                    Value = cacheHelper.Get<T>(Key);
                else
                {
                    if (typeof(T).IsGenericType) //如果T是泛型
                    {
                        var argType = typeof(T).GenericTypeArguments.FirstOrDefault();//LIST<T> 中T的type
                        Value = dal.GetType().GetMethod("GetList").MakeGenericMethod(new Type[] { argType }).Invoke(dal, new object[] { sqlWhere }) as T;
                    }
                    else
                        Value = dal.GetModel<T>(sqlWhere);

                    cacheHelper.Set(Key, Value, TimeSpan.FromHours(Hour));
                }
                return Value;
            }
            catch (Exception ex)
            {
                Log.Error("CacheHandle", ex);
                return null;
            }
        }


        public static async Task<string> FileUpload(string url, string base64Str)
        {
            return await Task.Run(() =>
            {
                var strImage = string.Empty;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                //request.ReadWriteTimeout = 30 * 1000;
                ///添加参数  
                Dictionary<String, String> dicList = new Dictionary<String, String>();

                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"Shared\": {}");
                sb.Append(",\"Data\": {\"Picture\": \"" + base64Str + "\"}");
                sb.Append("}");
                dicList.Add("request", sb.ToString());

                String postStr = buildQueryStr(dicList);
                byte[] data = Encoding.UTF8.GetBytes(postStr);
                request.ContentLength = postStr.Length;
                Stream myRequestStream = request.GetRequestStream();
                myRequestStream.Write(data, 0, data.Length);
                myRequestStream.Close();
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader myStreamReader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string retString = myStreamReader.ReadToEnd();
                JObject jsonObj = JObject.Parse(retString);
                //string retString = "{\"Result\":{\"HasError\":false,\"ErrorCode\":1,\"ErrorMessage\":\"\"},\"Data\":null}";
                if (jsonObj != null)
                {
                    if (jsonObj["Result"]["HasError"].ToString() == "False")
                    {
                        strImage = jsonObj["Data"]["FileName"].ToString();
                    }
                }
                myStreamReader.Close();
                return strImage;

            });

        }

        private static string buildQueryStr(Dictionary<String, String> dicList)
        {
            String postStr = "";
            foreach (var item in dicList)
            {
                postStr += item.Key + "=" + WebUtility.UrlEncode(item.Value) + "&";
            }
            postStr = postStr.Substring(0, postStr.LastIndexOf('&'));
            return postStr;
        }


        public static string UseMQ()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: "hello",
                                   durable: false,
                                   exclusive: false,
                                   autoDelete: false,
                                   arguments: null);

                    string message = "Hello World!";
                    var body = Encoding.UTF8.GetBytes(message);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "hello",
                                         basicProperties: null,
                                         body: body);
                }
            }
            return "Hello World！";
        }
    }
}
