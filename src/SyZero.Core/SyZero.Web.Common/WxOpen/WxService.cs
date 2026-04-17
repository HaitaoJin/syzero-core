using Newtonsoft.Json;
using RestSharp;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SyZero.Web.Common
{
    /// <summary>
    /// 微信公众号服务
    /// </summary>
    public class WxService
    {
        public string access_token { get; set; }
        public string appId { get; set; }
        public string appSecret { get; set; }
        public WxService(string AppId, string AppSecret)
        {
            this.appId = AppId;
            this.appSecret = AppSecret;
        }

        /// <summary>
        /// 获取公众号AccessToken
        /// </summary>
        /// <returns></returns>
        public async Task<WxAccessToken> GetWxToken()
        {
            WxAccessToken accessToken = new WxAccessToken();
            string grant_type = "client_credential";
            string wxacodeUrl = string.Format("https://api.weixin.qq.com/cgi-bin/token?grant_type={0}&appid={1}&secret={2}", grant_type, appId, appSecret);
            RestRequest request = new RestRequest(wxacodeUrl, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            //获取微信凭证
            var result = await RestHelper.ExecuteAsync(request);
            if (result == null) return accessToken;
            accessToken.access_token = result["access_token"].ToString();
            accessToken.expires_in = result["expires_in"].ToString();
            access_token = accessToken.access_token;
            Console.WriteLine("[获取小程序AccessToken]accessToken:" + accessToken.expires_in);
            return accessToken;
        }

        /// <summary>
        /// 查询自定义菜单
        /// </summary>
        /// <returns></returns>
        public async Task<SelfmenuInfoResult> GetCurrentSelfmenuInfo()
        {
            string wxacodeUrl = $"https://api.weixin.qq.com/cgi-bin/get_current_selfmenu_info?access_token={access_token}";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            var result = await RestHelper.ExecuteAsync<SelfmenuInfoResult>(request);
            return result;
        }

        /// <summary>
        /// 设置自定义菜单
        /// </summary>
        /// <returns></returns>
        public async Task<WxOpenJsonResult> SetSelfmenuInfo(CreateSelfmenuInfo info)
        {
            string wxacodeUrl = $"https://api.weixin.qq.com/cgi-bin/menu/create?access_token={access_token}";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var jsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var json = JsonConvert.SerializeObject(info, Formatting.Indented, jsonSetting);
            request.AddStringBody(json, DataFormat.Json);
            var result = await RestHelper.ExecuteAsync<WxOpenJsonResult>(request);
            return result;
        }

        /// <summary>
        /// 发送模板消息
        /// </summary>
        /// <returns></returns>
        public async Task<WxOpenJsonResult> SendTemplateMessage(WxTemplateMessage info)
        {
            string wxacodeUrl = $"https://api.weixin.qq.com/cgi-bin/message/template/send?access_token={access_token}";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var jsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var json = JsonConvert.SerializeObject(info, Formatting.Indented, jsonSetting);
            request.AddStringBody(json, DataFormat.Json);
            var result = await RestHelper.ExecuteAsync<WxOpenJsonResult>(request);
            return result;
        }

        /// <summary>
        /// 公众号登录获取OpenId
        /// </summary>
        /// <returns></returns>
        public async Task<WxAauthResult> Login(string code)
        {
            string grant_type = "authorization_code";
            string wxacodeUrl = $"https://api.weixin.qq.com/sns/oauth2/access_token?appid={appId}&secret={appSecret}&code={code}&grant_type={grant_type}";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            var result = await RestHelper.ExecuteAsync<WxAauthResult>(request);
            return result;
        }

        /// <summary>
        /// 通过openId获取用户信息
        /// </summary>
        /// <returns></returns>
        public async Task<WxUserInfoResult> GetUserInfo(string openId)
        {
            string wxacodeUrl = $"https://api.weixin.qq.com/cgi-bin/user/info?access_token={access_token}&openid={openId}&lang=zh_CN";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            var result = await RestHelper.ExecuteAsync<WxUserInfoResult>(request);
            return result;
        }


        /// <summary>
        /// 获取JSAPI加密参数
        /// </summary>
        /// <returns></returns>
        public async Task<WxJSAPIParametersResult> GetJSAPIParameters(string openurl)
        {
            string wxacodeUrl = $"https://api.weixin.qq.com/cgi-bin/ticket/getticket?access_token={access_token}&type=jsapi";
            RestRequest request = new RestRequest(wxacodeUrl, Method.Get);
            request.AddHeader("Content-Type", "application/json");
            var ticketResult = await RestHelper.ExecuteAsync<WxTicketResult>(request);
            if (ticketResult.errcode != WxOpenReturnCode.RequestSuccess)
            {
                return new WxJSAPIParametersResult()
                {
                    errcode = ticketResult.errcode,
                    errmsg = ticketResult.errmsg
                };
            }

            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            var timestamp = Convert.ToInt64(ts.TotalSeconds);
            var noncestr = Guid.NewGuid().ToString().Replace("-", "");
            var jmdata = "jsapi_ticket={0}&noncestr={1}&timestamp={2}&url={3}";
            jmdata = string.Format(jmdata, ticketResult.ticket, noncestr, ts, openurl);



            return new WxJSAPIParametersResult()
            {
                errcode = WxOpenReturnCode.RequestSuccess,
                ticket = ticketResult.ticket,
                noncestr = noncestr,
                signature = GetSignature(ticketResult.ticket, noncestr, timestamp, openurl),
                timestamp = timestamp.ToString(),
                url = openurl
            };
        }



        /// <summary>
        /// 签名算法
        /// </summary>
        /// <param name="jsapi_ticket">jsapi_ticket</param>
        /// <param name="noncestr">随机字符串(必须与wx.config中的nonceStr相同)</param>
        /// <param name="timestamp">时间戳(必须与wx.config中的timestamp相同)</param>
        /// <param name="url">当前网页的URL，不包含#及其后面部分(必须是调用JS接口页面的完整URL)</param>
        /// <returns></returns>
        public string GetSignature(string jsapi_ticket, string noncestr, long timestamp, string url)
        {
            var string1Builder = new StringBuilder();
            string1Builder.Append("jsapi_ticket=").Append(jsapi_ticket).Append("&")
                          .Append("noncestr=").Append(noncestr).Append("&")
                          .Append("timestamp=").Append(timestamp).Append("&")
                          .Append("url=").Append(url.IndexOf("#") >= 0 ? url.Substring(0, url.IndexOf("#")) : url);
            string string1 = string1Builder.ToString();

            var sha1 = new SHA1Managed();
            var sha1bytes = System.Text.Encoding.GetEncoding("UTF-8").GetBytes(string1);
            byte[] resultHash = sha1.ComputeHash(sha1bytes);
            string sha1String = BitConverter.ToString(resultHash).ToLower();
            sha1String = sha1String.Replace("-", "");

            return sha1String;
        }
    }
}
