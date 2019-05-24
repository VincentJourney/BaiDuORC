using BaiDuOCR.Model.Entity;
using Core.FrameWork;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using BaiDuOCR.Model;
using BaiDuOCR.Model.Util;
using BaiDuOCR.FrameWork;
using System.Reflection;
using BaiDuOCR.Core;

namespace BaiDuOCR.Core
{
    public static class VerifyRuleBLL
    {
        private static OCRDAL dal = new OCRDAL();

        private static CacheHelper cacheHelper = new CacheHelper();

        /// <summary>
        /// 默认最小时间
        /// </summary>
        private static readonly DateTime DefaultDateTime = DateTime.Parse("1900-01-01");

        public static async Task<Result> ReceiptOCR(string Id)
        {
            try
            {
                var ApplyPointModel = dal.GetModel<ApplyPoint>($"and ApplyPointID='{Id}'");        //根据Id 获取 积分申请
                if (ApplyPointModel == null)
                    return new Result(false, "积分申请表查无数据", null);

                var ReceiptOCRResult = await Task.Run(async () =>
                  {

                      #region 调用BAIDUOCR API，并将识别数据添加至原始数据表

                      var Image = (Bitmap)Bitmap.FromFile(ApplyPointModel.ReceiptPhoto);
                      var base64 = Commom.ImgToBase64String(Image);      //拿到积分申请中的图片转为Base64
                      base64 = System.Web.HttpUtility.UrlEncode(base64);
                      //byte[] bytes = Convert.FromBase64String(base64);
                      //var result = Commom.GeneralOCRBasic(s => s.Receipt(bytes));   //暂时为票据服务识别
                      //根据MallOCRRule配置的OCR相关配置进行OCR请求，暂不用SDK,缓存处理
                      MallOCRRule MallOCRRule = CacheHandle<MallOCRRule>($"MallOCRRule{Id}", 1, $" and MallID='{ApplyPointModel.MallID}'");
                      var result = HttpHelper.HttpPost(string.Format(MallOCRRule.OCRServerURL, MallOCRRule.OCRServerToken), $"image={base64}");
                      if (result.Contains("error_msg"))
                      {
                          var OCRErrorModel = JsonConvert.DeserializeObject<OCRErrorResult>(result);
                          return new Result(false, OCRErrorModel.error_msg, null);
                      }

                      var RecongnizeModel = new ApplyPictureRecongnize
                      {
                          id = Guid.NewGuid(),
                          applyid = Guid.Parse(Id),
                          Lineno = 0,
                          LineContent = result
                      };
                      var AddResult = DbContext.Add(RecongnizeModel);     //识别内容添加到识别原始表

                      #endregion

                      if (AddResult == 0)
                      {
                          ApplyPointModel.RecongizeStatus = 1;
                          var UpadateResult = DbContext.Update(ApplyPointModel);      //已解析原始数据
                          if (UpadateResult)
                          {
                              //根据商铺规则明细取到OCR结果
                              var ApplyOCRResult = GetApplyPointOCRResult(Guid.Parse(Id), ApplyPointModel.StoreID, result);
                              if (ApplyOCRResult.Success)
                              {
                                  ApplyPointOCRResult OCRResult = (ApplyPointOCRResult)ApplyOCRResult.Data;

                                  //匹配商铺规则
                                  var VerifyResult = VerifyOCRResult(OCRResult);
                                  if (VerifyResult.Success)
                                  {
                                      //更新积分申请表
                                      ApplyPointModel.RecongizeStatus = 2;//成功匹配
                                      ApplyPointModel.AuditDate = DateTime.Now;//修改审批日期
                                      if (DbContext.Update(ApplyPointModel))
                                      {
                                          Store StoreModel = CacheHandle<Store>($"Store{ApplyPointModel.StoreID}", 1, $" and StoreId = '{ApplyPointModel.StoreID}'");
                                          OrgInfo orgInfo = CacheHandle<OrgInfo>($"OrgInfo{StoreModel.OrgID}", 24, $" and OrgId = '{StoreModel.OrgID}'");
                                          Company company = CacheHandle<Company>($"Company{StoreModel.CompanyID}", 24, $" and CompanyId = '{StoreModel.CompanyID}'");

                                          Card card = dal.GetModel<Card>($" and CardID = '{ApplyPointModel.CardID}'");
                                          //自动积分     
                                          var webPosArg = new WebPosArg
                                          {
                                              cardID = card.CardCode,
                                              companyID = company.CompanyCode,
                                              orgID = orgInfo.OrgCode,
                                              storeID = StoreModel.StoreCode,
                                              cashierID = "crm",
                                              discountPercentage = 0,
                                              receiptNo = ApplyPointModel.ReceiptNo,
                                              txnDateTime = ApplyPointModel.TransDate
                                          };
                                          var webPosResult = await WebPosForPoint(webPosArg);
                                          return webPosResult;
                                      }
                                      else
                                          return new Result(false, "OCR信息校验成功后修改积分申请表失败", null);

                                  }
                                  else
                                      return new Result(false, VerifyResult.Message, null);
                              }
                              else
                              {
                                  ApplyPointModel.RecongizeStatus = 3;//成功匹配
                                  DbContext.Update(ApplyPointModel);
                                  return new Result(false, ApplyOCRResult.Message, null);
                              }
                          }
                          else
                              return new Result(false, "修改积分申请表失败", null);
                      }
                      else
                          return new Result(false, "添加到原始表失败", null);
                  });

                return ReceiptOCRResult;
            }
            catch (Exception ex)
            {
                Log.Error("ReceiptOCR", ex);
                return new Result(false, ex.Message, null);
            }

        }
        /// <summary>
        /// 根据商铺规则明细取到内容
        /// </summary>
        /// <param name="ApplyId">积分申请单ID</param>
        /// <param name="StoreId">商铺Id</param>
        /// <param name="OCRResult">OCR识别原始数据</param>
        /// <returns></returns>
        private static Result GetApplyPointOCRResult(Guid ApplyId, Guid StoreId, string OCRResult)
        {
            try
            {   //缓存处理
                StoreOCR StoreOCRRule = CacheHandle<StoreOCR>($"StoreOCR{StoreId}", 0.5, $"and StoreId = '{StoreId.ToString()}'");
                if (StoreOCRRule == null)
                {
                    return new Result(false, "商铺未设置自动积分规则", null);
                }
                if (StoreOCRRule.Enabled != 1)
                {
                    return new Result(false, "商铺未启用自动积分", null);
                }

                //当商铺启用自动积分
                var OCRResultModel = JsonConvert.DeserializeObject<OCRResult>(OCRResult);//识别的内容
                if (OCRResultModel == null)
                {
                    return new Result(false, "OCR识别失败", null);
                }

                List<StoreOCRDetail> StoreOCRDetailRuleList = CacheHandle<List<StoreOCRDetail>>($"StoreOCRDetail{StoreId}", 0.5, $"and StoreId = '{StoreId.ToString()}'");
                if (StoreOCRDetailRuleList == null)
                {
                    return new Result(false, "商铺未设置自动规则明细", null);
                }

                ApplyPointOCRResult applyPointOCRResult = new ApplyPointOCRResult //积分申请识别结果
                {
                    id = Guid.NewGuid(),
                    applyid = ApplyId,
                    StoreID = StoreId,
                    VerifyStatus = 0,
                    Status = 0,
                    needVerify = StoreOCRRule.needVerify,
                    StoreCode = "",
                    ReceiptNo = "",
                    TransDatetime = DefaultDateTime,
                    TranAmount = 0,
                };

                //1 异步 明细数量个线程  识别  ???


                foreach (var StoreDetailRule in StoreOCRDetailRuleList) //遍历商铺规则明细
                {
                    for (int i = 0; i < OCRResultModel.words_result.Count(); i++) //根据规则循环匹配识别文字
                    {
                        Result ReturnResult = GetValue(OCRResultModel.words_result, i, StoreDetailRule); //根据规则取值
                        if (ReturnResult.Success)
                        {
                            var ReturnData = ReturnResult.Data.ToString();
                            switch (StoreDetailRule.OCRKeyType) //枚举有注释，根据关键字类型赋值
                            {
                                case (int)OCRKeyType.StoreNo:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && string.IsNullOrWhiteSpace(applyPointOCRResult.StoreCode))
                                    {
                                        applyPointOCRResult.StoreCode = dal.GetModel<Store>($" and StoreName like '%{ReturnData}%'")?.StoreCode ?? "";
                                        continue;
                                    }
                                    break;
                                case (int)OCRKeyType.ReceiptNO:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && string.IsNullOrWhiteSpace(applyPointOCRResult.ReceiptNo))
                                    {
                                        applyPointOCRResult.ReceiptNo = ReturnResult.Data.ToString();
                                        continue;
                                    }
                                    break;
                                case (int)OCRKeyType.DateTime:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && applyPointOCRResult.TransDatetime == DefaultDateTime)
                                    {
                                        ReturnData = ReturnData.Replace(" ", "").Insert(10, " ");//可能会识别成 2019 - 05 - 1512:14:44 转datetime 报错
                                        if (DateTime.TryParse(ReturnData, out var DateTimeResult))
                                        {
                                            applyPointOCRResult.TransDatetime = DateTimeResult;
                                            continue;
                                        }
                                    }
                                    break;
                                case (int)OCRKeyType.Amount:
                                    if (!string.IsNullOrWhiteSpace(ReturnData) && applyPointOCRResult.TranAmount == 0)
                                    {
                                        if (decimal.TryParse(ReturnResult.Data.ToString(), out var AmountResult))
                                        {
                                            applyPointOCRResult.TranAmount = AmountResult;
                                            continue;
                                        }
                                    }
                                    break;
                                default:
                                    return new Result(false, $"商铺未设置该关键字类型取值方法：{StoreDetailRule.OCRKeyType}", null);
                            }
                        }
                        else
                        {
                            return ReturnResult;
                        }
                    }
                }
                return new Result(true, "匹配规则成功", applyPointOCRResult);




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
            catch (Exception ex)
            {
                return new Result(false, ex.Message, null);
            }
        }

        /// <summary>
        /// 小票OCR结果匹配商铺自动积分规则
        /// </summary>
        /// <param name="OCRResult"></param>
        /// <returns></returns>
        private static Result VerifyOCRResult(ApplyPointOCRResult OCRResult)
        {
            //是否全部识别
            if (string.IsNullOrWhiteSpace(OCRResult.StoreCode) || OCRResult.TranAmount == 0 || OCRResult.TransDatetime == DefaultDateTime || string.IsNullOrWhiteSpace(OCRResult.ReceiptNo))
                OCRResult.Status = 1;
            else
                OCRResult.Status = 2;

            if (DbContext.Add(OCRResult) == 0) //添加至积分申请识别结果表
            {
                if (OCRResult.needVerify == 1) //当需要需要校验 
                {
                    var ApplyPointModel = dal.GetModel<ApplyPoint>($"and ApplyPointID='{OCRResult.applyid}'");

                    Store StoreModel = CacheHandle<Store>($"Store{OCRResult.StoreID}", 1, $" and StoreId = '{OCRResult.StoreID}'");
                    if (string.IsNullOrWhiteSpace(OCRResult.StoreCode) || StoreModel.StoreCode != OCRResult.StoreCode)
                        return new Result(false, "OCR店铺Code未识别正确！", null);

                    if (OCRResult.TransDatetime == DefaultDateTime && OCRResult.TransDatetime.ToString("yyyy-MM-dd") != ApplyPointModel.TransDate.ToString("yyyy-MM-dd"))
                        return new Result(false, "OCR交易日期未识别正确！", null);

                    StoreOCR StoreOCRRule = CacheHandle<StoreOCR>($"StoreOCR{OCRResult.StoreID}", 0.5, $"and StoreId = '{OCRResult.StoreID.ToString()}'");

                    if (OCRResult.TranAmount < StoreOCRRule.MinValidReceiptValue || OCRResult.TranAmount > StoreOCRRule.MaxValidReceiptValue)
                        return new Result(false, "OCR小票金额不在店铺规则范围之内！", null);

                    if (OCRResult.TranAmount != ApplyPointModel.TransAmt)
                        return new Result(false, "OCR小票金额不一致！", null);

                    if (StoreOCRRule.MaxTicketPerDay != 0)  //日自动交易笔数为0时 代表不限制
                    {
                        var TicketPerDay = DbContext.Query<int>($@"select count(*) from ApplyPoint
                               where StoreID = '{OCRResult.StoreID.ToString()}' and VerifyStatus = 1 
                               and SourceType=7 and DATEDIFF(dd,AuditDate,GETDATE())=0").FirstOrDefault(); //当日交易笔数
                        if (TicketPerDay >= StoreOCRRule.MaxTicketPerDay)
                            return new Result(false, "今日已超过最大自动积分记录数量", null);
                    }

                    if ((StoreModel.IsStandardPOS == "1" ? 0 : 1) != StoreOCRRule.POSType)
                        return new Result(false, "OCR商铺POS类型不一致", null);

                    MallOCRRule MallOCRRule = CacheHandle<MallOCRRule>($"MallOCRRule{OCRResult.StoreID}", 1, $" and MallID='{StoreModel.MallID}'");

                    var posUrl = MallOCRRule.POSServeURL;
                    var posToken = MallOCRRule.POSServerToken;
                    var posUser = MallOCRRule.POSServerUser;
                    var userPwd = MallOCRRule.POSServerPassword;

                    //请求POS POSSID
                    //if (POSSID != StoreOCRRule.POSSID)
                    //{
                    //    return new Result(false, "OCR商铺POS代码不一致", null);
                    //}

                }
            }
            else
                return new Result(false, "ApplyPointOCRResult添加失败", null);


            OCRResult.VerifyStatus = 1;
            if (DbContext.Update(OCRResult))
                return new Result(true, "", null);
            else
                return new Result(false, "ApplyPointOCRResult修改VerifyStatus失败", null);

        }


        /// <summary>
        /// 自动积分并微信推送接口
        /// </summary>
        /// <param name="webPosArg"></param>
        /// <returns></returns>
        private async static Task<Result> WebPosForPoint(WebPosArg webPosArg)
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



    }
}
