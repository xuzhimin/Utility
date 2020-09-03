通过PuppeteerSharp调用chrome.exe将html网址存为pdf

要求服务器为windows2012以上、FrameWork4.6.1以上，服务器安装chrome
将Publish发布到IIS中，应用程序池选择4.0集成模式即可

web.config中配置chromepath为chrome.exe所在路径，将chrome.exe所在文件夹复制到非C盘，防止C盘无访问权限
pdfpath为缓存pdf所在路径


使用方式，发布到iis后，将待转的网址通过base64加密后，使用get请求访问即可，如：http://127.0.0.1:8080/xxxxxx