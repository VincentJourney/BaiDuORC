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
using BaiDuOCR.Request;
using BaiDuOCR.Attr;
using BaiDuOCR.Model.Response;

namespace BaiDuOCR.Core
{
    public static class OCRVerify
    {
        private static OCRDAL dal = new OCRDAL();

        private static CacheHelper cacheHelper = new CacheHelper();

        /// <summary>
        /// 缓存锁  避免并发设置缓存增加DB压力
        /// </summary>
        private static readonly Object cacheLocker = new object();

        /// <summary>
        /// 图片识别
        /// </summary>
        /// <param name="cardId"></param>
        /// <param name="mallId"></param>
        /// <param name="base64"></param>
        /// <returns>识别结果</returns>
        public static async Task<Result> ReceiptOCR(OCRRequest oCRRequest)
        {
            return await Task.Run(() =>
             {
                 var OcrResult = BaiDuReceiptOCR(oCRRequest);
                 if (OcrResult.Success)
                     return RecognitOCRResult(OcrResult.Data.ToString());
                 else
                     return OcrResult;
             });
        }

        /// <summary>
        /// 调用百度OCR小票识别服务 返回原始数据
        /// </summary>
        /// <returns></returns>
        public static Result BaiDuReceiptOCR(OCRRequest oCRRequest)
        {
            try
            {
                var value = ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:AllMallOCRRule");
                var hour = double.Parse(value);

                List<MallOCRRule> MallRuleList = CacheHandle<List<MallOCRRule>>(
                   Key: "AllMallOCRRule",
                   Hour: int.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:AllMallOCRRule")),
                   sqlWhere: "");

                var MallRuleModel = MallRuleList.Where(s => s.MallID == Guid.Parse(oCRRequest.mallId)).FirstOrDefault();
                if (MallRuleModel == null)
                    return new Result(false, "请定义Mall的OCR规则", null);

                var result = HttpHelper.HttpPost(string.Format(MallRuleModel.OCRServerURL, MallRuleModel.OCRServerToken), $"image={HttpUtility.UrlEncode(oCRRequest.base64)}");
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
                List<StoreOCRDetail> AllStoreOCRDetailRuleList = CacheHandle<List<StoreOCRDetail>>(
                    Key: "AllStoreOCRDetailRuleList"
                    , Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:AllStoreOCRDetailRuleList"))
                    , sqlWhere: "");

                //所有商铺名称规则
                var StoreNameRuleList = AllStoreOCRDetailRuleList.Where(s => s.OCRKeyType == (int)OCRKeyType.StoreName).Select(c => c.OCRKey).ToList();

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
                            if (!string.IsNullOrWhiteSpace(ReturnData))
                            {
                                switch (StoreDetailRule.OCRKeyType) //枚举有注释，根据关键字类型赋值
                                {
                                    case (int)OCRKeyType.StoreName:
                                        continue;
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
                }

                var RecongnizeModelId = Guid.NewGuid();
                var RecongnizeModel = new ApplyPictureRecongnize
                {
                    id = RecongnizeModelId,
                    applyid = Guid.NewGuid(),
                    Lineno = 0,
                    LineContent = JsonConvert.SerializeObject(WordList),
                    OCRResult = JsonConvert.SerializeObject(ReceiptOCRModel)
                };
                var AddResult = DbContext.Add(RecongnizeModel);
                //添加成功后 出参RecongnizeModelId
                if (AddResult == 0)
                    ReceiptOCRModel.RecongnizelId = RecongnizeModelId;
                else
                    return new Result(false, "添加到ApplyPictureRecongnize失败", null);

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
            StoreOCR StoreOCRRule = CacheHandle<StoreOCR>(
               Key: $"StoreOCR{receiptOCR.StoreId}",
               Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:StoreOCR")),
               sqlWhere: $"and StoreId = '{receiptOCR.StoreId}'");

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

                Store StoreModel = CacheHandle<Store>(
                    Key: $"Store{receiptOCR.StoreId}",
                    Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:Store")),
                    sqlWhere: $" and StoreId = '{receiptOCR.StoreId}'");

                if ((StoreModel.IsStandardPOS == "1" ? 0 : 1) != StoreOCRRule.POSType)
                    return new Result(false, "OCR商铺POS类型不一致", null);

                //POS门店代码暂无验证
            }
            #endregion

            return new Result(true, "验证成功", null);
        }

        /// <summary>
        /// 创建积分申请单，校验信息成功并推送
        /// 若原先存在积分申请单，失败的原因：校验失败 所有应该重新赋值
        /// </summary>
        /// <param name="cardId"></param>
        /// <param name="receiptOCR"></param>
        /// <returns></returns>
        public static Result CreateApplyPoint(ApplyPointRequest applyPointRequest)
        {
            try
            {
                #region 图片上传
                ImageResponse ImageResponse = null;
                try
                {
                    var FileUploadResult = ImageUpload(Commom.FileUploadUrl, applyPointRequest.receiptOCR.Base64);
                    if (!FileUploadResult.Success)
                        return FileUploadResult;
                    ImageResponse = JsonConvert.DeserializeObject<ImageResponse>(FileUploadResult.Data.ToString());
                }
                catch (Exception ex)
                {
                    Log.Error("CreateApplyPoint-FileUpload", ex);
                    return new Result(false, ex.Message, null);
                }
                #endregion

                Store StoreModel = CacheHandle<Store>(
                    Key: $"Store{applyPointRequest.receiptOCR.StoreId}",
                    Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:Store")),
                    sqlWhere: $" and StoreId = '{applyPointRequest.receiptOCR.StoreId}'");

                StoreOCR StoreOCRRule = CacheHandle<StoreOCR>(
                    Key: $"StoreOCR{applyPointRequest.receiptOCR.StoreId}",
                    Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:StoreOCR")),
                    sqlWhere: $"and StoreId = '{applyPointRequest.receiptOCR.StoreId}'");

                var ApplyPoint = dal.GetModel<ApplyPoint>($" and ReceiptNo='{applyPointRequest.receiptOCR.ReceiptNo}' and StoreID='{applyPointRequest.receiptOCR.StoreId}'");


                var IsHas = false;// 是否原先存在积分申请单  默认没有
                if (ApplyPoint == null)  //判断该小票号 是否存在积分申请单 不存在则添加原始积分申请单 （已解析原始数据，校验失败）
                {
                    var ApplyPointId = Guid.NewGuid();
                    ApplyPoint = new ApplyPoint
                    {
                        ApplyPointID = ApplyPointId,
                        MallID = StoreModel.MallID,
                        StoreID = applyPointRequest.receiptOCR.StoreId,
                        CardID = Guid.Parse(applyPointRequest.cardId),
                        ReceiptNo = applyPointRequest.receiptOCR.ReceiptNo,
                        TransDate = applyPointRequest.receiptOCR.TransDatetime,
                        TransAmt = applyPointRequest.receiptOCR.TranAmount,
                        VerifyStatus = StoreOCRRule.needVerify == 0 ? 1 : 0,
                        ReceiptPhoto = ImageResponse.fileURL
                    };
                }
                else
                {
                    IsHas = true;
                    ApplyPoint.MallID = StoreModel.MallID;
                    ApplyPoint.StoreID = applyPointRequest.receiptOCR.StoreId;
                    ApplyPoint.CardID = Guid.Parse(applyPointRequest.cardId);
                    ApplyPoint.ReceiptNo = applyPointRequest.receiptOCR.ReceiptNo;
                    ApplyPoint.TransDate = applyPointRequest.receiptOCR.TransDatetime;
                    ApplyPoint.TransAmt = applyPointRequest.receiptOCR.TranAmount;
                    ApplyPoint.VerifyStatus = StoreOCRRule.needVerify == 0 ? 1 : 0;
                }

                var VerifyRecognitionResult = VerifyRecognition(applyPointRequest.receiptOCR);//校验结果
                ApplyPoint.AuditDate = DateTime.Now;
                if (VerifyRecognitionResult.Success)//校验成功
                {
                    ApplyPoint.RecongizeStatus = 2;
                    ApplyPoint.VerifyStatus = 1;
                }
                else//校验失败 修改值
                {
                    ApplyPoint.RecongizeStatus = 3;
                    ApplyPoint.VerifyStatus = 0;
                    ApplyPoint.Status = 0;
                    if (IsHas)
                        DbContext.Update<ApplyPoint>(ApplyPoint);
                    else
                        DbContext.Add<ApplyPoint>(ApplyPoint);
                    return VerifyRecognitionResult;
                }

                var LastRes = true;//添加是否成功 
                if (IsHas)
                {
                    if (!DbContext.Update<ApplyPoint>(ApplyPoint))
                        LastRes = false;
                }
                else
                {
                    if (DbContext.Add<ApplyPoint>(ApplyPoint) != 0)
                        LastRes = false;
                }

                if (LastRes)
                {
                    List<Company> companyList = CacheHandle<List<Company>>(
                        Key: "Company",
                        Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:Company")),
                        sqlWhere: "");
                    List<OrgInfo> orgList = CacheHandle<List<OrgInfo>>(
                        Key: "OrgInfo",
                        Hour: double.Parse(ConfigurationUtil.GetSection("ObjectConfig:CacheExpiration:OrgInfo")),
                        sqlWhere: "");

                    //自动积分 推送
                    var arg = new WebPosArg
                    {
                        companyID = companyList.Where(s => s.CompanyId == StoreModel.CompanyID).FirstOrDefault()?.CompanyCode ?? "",
                        storeID = StoreModel.StoreCode,
                        cardID = dal.GetModel<Card>($" and CardID='{applyPointRequest.cardId}'")?.CardCode ?? "",
                        cashierID = "CrmApplyPoint",
                        discountPercentage = 0,
                        orgID = orgList.Where(s => s.OrgId == StoreModel.OrgID).FirstOrDefault()?.OrgCode ?? "",
                        receiptNo = applyPointRequest.receiptOCR.ReceiptNo,
                        txnDateTime = applyPointRequest.receiptOCR.TransDatetime
                    };
                    var argStr = JsonConvert.SerializeObject(arg);
                    ProducerMQ(argStr);
                    return new Result(true, "校验成功，已申请积分！", null);
                }
                return new Result(false, "校验成功，对ApplyPoint操作失败！", null);
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
            var oldInfo = oldModel.GetType().GetProperties();
            var newInfo = newModel.GetType().GetProperties();

            if (oldInfo.Count() == 0 || newInfo.Count() == 0)
                return new Result(false, "Entity Data Error", null);

            foreach (var item in oldInfo)
            {
                var attr = (ValidateAttr)item.GetCustomAttribute(typeof(ValidateAttr));
                if (attr != null && attr.IgnoreKey == item.Name)
                    continue;
                var newinfoObj = newInfo.Where(s => s.Name == item.Name).FirstOrDefault();
                if (newinfoObj == null && !item.GetValue(oldModel, null).ToString().Contains(newinfoObj.GetValue(newModel, null).ToString()))
                    return new Result(false, "OCR数据与提交数据不一致", null);
            }
            return new Result(true, "", null);

        }

        /// <summary>
        /// 查询key-value是否存在，不存在则select DB 线程安全
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
                        lock (cacheLocker)//避免缓存并发
                        {
                            if (!cacheHelper.Exists(Key))
                            {
                                var argType = typeof(T).GenericTypeArguments.FirstOrDefault();//LIST<T> 中T type
                                Value = dal.GetType().GetMethod("GetList").MakeGenericMethod(new Type[] { argType }).Invoke(dal, new object[] { sqlWhere }) as T;
                            }
                        }
                    }
                    else
                    {
                        lock (cacheLocker)
                        {
                            if (!cacheHelper.Exists(Key))
                                Value = dal.GetModel<T>(sqlWhere);
                        }
                    }
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

        /// <summary>
        /// 图片上传
        /// </summary>
        /// <param name="url"></param>
        /// <param name="base64Str"></param>
        /// <returns></returns>
        public static Result ImageUpload(string url, string base64Str)
        {
            try
            {
                ImageRequest request = new ImageRequest()
                {
                    fileName = Guid.NewGuid().ToString(),
                    base64Str = base64Str,
                    sourceSystem = "crm-ocr",
                    fileDescription = "资源上传图片"
                };
                var result = HttpHelper.FileUpload(url, JsonConvert.SerializeObject(request));
                return new Result(true, "", result);

            }
            catch (Exception ex)
            {
                Log.Error("ImageUpload", ex);
                return new Result(false, ex.Message, null);
            }
        }


        /// <summary>
        /// MQ生产者
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static string ProducerMQ(string Value)
        {
            var factory = new ConnectionFactory()
            {
                HostName = ConfigurationUtil.GetSection("ObjectConfig:RabbitMqConfig:HostName"),
                Port = int.Parse(ConfigurationUtil.GetSection("ObjectConfig:RabbitMqConfig:Port")),
                UserName = ConfigurationUtil.GetSection("ObjectConfig:RabbitMqConfig:UserName"),
                Password = ConfigurationUtil.GetSection("ObjectConfig:RabbitMqConfig:Password"),
            };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "apply_queue",
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);


                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                var body = Encoding.UTF8.GetBytes(Value);
                channel.BasicPublish(exchange: "",
                                     routingKey: "apply_queue",
                                     basicProperties: properties,
                                     body: body);
            }
            return "Hello World!";
        }
    }
}
