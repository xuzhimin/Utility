using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

namespace Power.UrlToPDF.API.Controllers
{
    public class UrlToPDFController : ApiController
    {
        public async System.Threading.Tasks.Task<string> Get(string id)
        { 
            string urlPath = base64Decode(id);
            return  await LoadPDF(urlPath);
        }
        public async System.Threading.Tasks.Task<string> LoadPDF(string url)
        {
             Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = System.Configuration.ConfigurationManager.AppSettings["chromepath"]
            });
            string fileName = Guid.NewGuid().ToString() + ".pdf";
            try
            {
                Page page = await browser.NewPageAsync();
                // await page.GoToAsync(url);
                await page.GoToAsync(url, new NavigationOptions { WaitUntil = new WaitUntilNavigation[] { WaitUntilNavigation.Networkidle2 } });
                // await page.WaitForNavigationAsync(new NavigationOptions {  WaitUntil=new WaitUntilNavigation[] { WaitUntilNavigation.Load } });
                string path = System.Configuration.ConfigurationManager.AppSettings["pdfpath"];
                if (path.LastIndexOf("\\") == path.Length - 1)
                    path = path.Substring(0, path.Length - 1);
                path += "\\";
                await page.PdfAsync(path + fileName, new PdfOptions()
                {
                    PrintBackground = true,
                    MarginOptions = new PuppeteerSharp.Media.MarginOptions()
                    {
                        Left = "10mm",
                        Right = "10mm",
                        Bottom = "10mm",
                        Top = "10mm",
                    },
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    Landscape = false//纵向

                });
                NewLife.Log.XTrace.WriteLine("生成附件成功：" + fileName);
            }
            catch (Exception ex)
            {
                NewLife.Log.XTrace.WriteLine("生成附件失败：" + ex.Message);
            }
            finally
            {
                await browser.CloseAsync();
            }
            return fileName ;
        }
        /// <summary>             
        /// Base64解码
        /// </summary>  
        /// <param name="data"></param>   
        /// <returns></returns>       
        private string base64Decode(string data)
        {
            if (string.IsNullOrEmpty(data) == true)
                return "";

            try
            {
                data = data.Replace("%2B", "+");
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();
                byte[] todecode_byte = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch (Exception )
            {
                //throw new Exception("Error in base64Decode" + e.Message);
                return null;
            }
        }
    }
}
