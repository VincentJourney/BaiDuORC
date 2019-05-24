using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Drawing;
using Core.FrameWork;
using BaiDuOCR.Model.Entity;
using BaiDuOCR.Core;
using BaiDuOCR.Model.Util;
using System.Diagnostics;

namespace BaiDuOCR.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ImageOCRController : ControllerBase
    {

        public static readonly string webRoot = AppContext.BaseDirectory.Split("\\bin\\")[0];

        public ObjectConfig Setting;
        public ImageOCRController(IOptions<ObjectConfig> option)
        {
            Setting = option.Value;
        }

        [HttpGet]
        public string Get()
        {
            return JsonConvert.SerializeObject(OCRVerify.UseMQ());
        }


        [HttpPost("OCRVersionOne")]   //暂时测试 applyPointID ： 25E4DF19-F956-41FE-B935-0FB2AF3501B0
        public async Task<string> OCRVersionOne(string Id)
        {
            try
            {
                Stopwatch a = new Stopwatch();
                a.Start();
                Result result = await VerifyRuleBLL.ReceiptOCR(Id);
                if (result.Success)
                {
                    await WebPosForPoint();
                }
                a.Stop();
                var b = a.Elapsed;
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                Log.Error("ReceiptOCR", ex);
                throw new Exception(ex.Message);
            }

        }

        [HttpPost("api/test")]
        public async Task<string> WebPosForPoint()
        {

            string testString = "<cmd type=\"SALES\" appCode=\"POS\"><shared offline=\"true\"><companyID>c001</companyID><orgID>12</orgID><storeID>12L102N1</storeID><cashierID>brady-crm</cashierID></shared><sales discountPercentage=\"0.90\" cardID=\"8888513\" txnDateTime=\"2018-03-21\" receiptNo=\"12L102N10120180321uytu7t876896{0}\" actualAmount=\"{1}\" payableAmount=\"{1}\" verificationCode=\"\" /></cmd>";
            int amount = new Random(((unchecked((int)DateTime.Now.Millisecond + (int)DateTime.Now.Ticks)))).Next(10, 2000);
            testString = string.Format(testString, amount, amount);

            WebPosTest.WebPOSSoapClient ws = new WebPosTest.WebPOSSoapClient(WebPosTest.WebPOSSoapClient.EndpointConfiguration.WebPOSSoap);
            WebPosTest.WebPOSCredentials mwc = new WebPosTest.WebPOSCredentials();
            mwc.Username = "pos";
            mwc.Password = ws.GetEncryptedCharAsync("8888").Result;
            try
            {
                var cmdResult = await ws.CmdAsync(mwc, testString);
                if (!string.IsNullOrWhiteSpace(cmdResult.CmdResult) && cmdResult.CmdResult.Contains("hasError=\"false\""))
                {
                    return "1";
                }
                else
                {
                    return "0";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;

            }

        }

        //图片识别
        [HttpPost("OCR")]
        public async Task<string> RecognitOCRResult(string mallId, string base64)
        {
            try
            {
                Stopwatch a = new Stopwatch();
                a.Start();
                Result result = await OCRVerify.ReceiptOCR(mallId, base64);
                //if (result.Success)
                //{
                //    await WebPosForPoint();
                //}
                a.Stop();
                //var b = a.Elapsed;
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                Log.Error("ReceiptOCR", ex);
                throw new Exception(ex.Message);
            }
        }

        //积分申请
        [HttpPost("ApplyPoint")]
        public async Task<string> ApplyPoint(string cardId, ReceiptOCR receiptOCR)
        {
            try
            {
                Stopwatch a = new Stopwatch();
                a.Start();
                Result result = await OCRVerify.CreateApplyPoint(cardId, receiptOCR);
                a.Stop();
                var b = a.Elapsed;
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                Log.Error("ReceiptOCR", ex);
                throw new Exception(ex.Message);
            }
        }

        [HttpPost("TestFileUpLoad")]
        public async Task<string> TestFileUpLoad([FromBody] string Base64)
        {
            var sw = new Stopwatch();
            sw.Start();
            var result = await OCRVerify.FileUpload("http://10.0.8.6:1848", Base64);
            sw.Stop();
            var b = sw.Elapsed;
            return result;
        }















    }
}