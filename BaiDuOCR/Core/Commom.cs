using Core.FrameWork;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Baidu.Aip.Ocr;
using Newtonsoft.Json.Linq;

namespace BaiDuOCR.Core
{
    public static class Commom
    {

        public static readonly string webRoot = AppContext.BaseDirectory.Split("\\bin\\")[0];
        public static readonly string Folder = ConfigurationUtil.GetSection("ObjectConfig:CleanFilesInfo:Folder");
        public static readonly string SaveDay = ConfigurationUtil.GetSection("ObjectConfig:CleanFilesInfo:SaveDay");
        public static readonly string AppKey = ConfigurationUtil.GetSection("ObjectConfig:BaiDuSetting:AppKey");
        public static readonly string AppSecret = ConfigurationUtil.GetSection("ObjectConfig:BaiDuSetting:AppSecret");
        public static readonly string FileUploadUrl = ConfigurationUtil.GetSection("ObjectConfig:FileUploadUrl");
        public static readonly DateTime DefaultDateTime = DateTime.Parse("1900-01-01");


        /// <summary>
        /// 调用通用文字识别
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns>OCRResult</returns>
        public static string GeneralOCRBasic(Func<Ocr, JObject> func)
        {
            try
            {
                var ocr = new Ocr(AppKey, AppSecret);
                return JsonConvert.SerializeObject(func(ocr));
            }
            catch (Exception ex)
            {
                Log.Error("图片ORC错误", ex);
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Bitmap => Base64
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        public static string ImgToBase64String(Bitmap bmp)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] arr = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(arr, 0, (int)ms.Length);
                ms.Close();
                return Convert.ToBase64String(arr);
            }
            catch (Exception ex)
            {
                Log.Error("图片转base64错误", ex);
                return ex.Message;
            }
        }

        /// <summary>
        /// 主动清理   
        /// 暂时测试  
        /// 提高性能可将数组拆分为多个线程进行删除
        /// </summary>
        /// <param name="fileDirect"></param>
        /// <param name="saveDay"></param>
        public static void DeleteFile()
        {
            DateTime nowTime = DateTime.Now;
            var FolderName = $"{webRoot}{Folder}";
            DirectoryInfo root = new DirectoryInfo(FolderName); //文件夹名
            var files = root.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                var timeSpan = (nowTime - files[i].CreationTime).Days;
                if (timeSpan > int.Parse(SaveDay))
                {
                    Directory.Delete(files[i].FullName, true);  //删除超过时间的文件夹
                }

            };
        }

        /// <summary>
        /// 压缩图片
        /// </summary>
        /// <param name="name">需要压缩的原图</param>
        /// <param name="width">压缩后的宽度</param>
        /// <param name="xDpi">分辨率</param>
        /// <param name="yDpi">分辨率</param>
        /// <returns></returns>
        public static string GetImage(string name, int width, int xDpi, int yDpi)
        {
            try
            {
                var appPath = $"{webRoot}/File/Image/";
                var errorImage = appPath + "404.png";//没有找到图片
                var imgPath = string.IsNullOrEmpty(name) ? errorImage : appPath + name;

                var imgTypeSplit = name.Split('.');
                var imgType = imgTypeSplit[imgTypeSplit.Length - 1].ToLower();

                //图片不存在
                if (!new FileInfo(imgPath).Exists)
                {
                    imgPath = errorImage;
                }

                //缩小图片
                using (var imgBmp = new Bitmap(imgPath))
                {
                    //找到新尺寸
                    var oWidth = imgBmp.Width;
                    var oHeight = imgBmp.Height;
                    var height = oHeight;
                    if (width > oWidth)
                    {
                        width = oWidth;
                    }
                    else
                    {
                        height = width * oHeight / oWidth;
                    }
                    var newImg = new Bitmap(imgBmp, width, height);
                    newImg.SetResolution(xDpi, yDpi);

                    var imgName = $"/File/UploadImage/{Guid.NewGuid()}.png";
                    var url = $"{webRoot}{imgName}";
                    newImg.Save(url, ImageFormat.Png);

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        data = url
                    });
                }

            }
            catch (Exception ex)
            {
                Log.Error("图片压缩错误", ex);
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    data = ""
                });
            }
        }


    }
}
