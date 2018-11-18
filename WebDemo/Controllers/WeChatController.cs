﻿using WebDemo.Model;
using WebDemo.WeChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.WebSockets;
using Newtonsoft.Json;

namespace WebDemo.Controllers
{
    /// <summary>
    /// 微信模块
    /// </summary>
    [RoutePrefix("api/wechat")]
    [Error]
    public class WeChatController : ApiController
    {
        private static Dictionary<string, DicSocket> _dicSockets = new Dictionary<string, DicSocket>();

        /// <summary>
        /// 创建websocket连接
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("socket/connect")]
        public HttpResponseMessage Connect()
        {
            HttpContext.Current.AcceptWebSocketRequest(ProcessRequest); //在服务器端接受Web Socket请求，传入的函数作为Web Socket的处理函数，待Web Socket建立后该函数会被调用，在该函数中可以对Web Socket进行消息收发
            return Request.CreateResponse(HttpStatusCode.SwitchingProtocols); //构造同意切换至Web Socket的Response.
        }

        /// <summary>
        /// websocket监听
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task ProcessRequest(AspNetWebSocketContext context)
        {
            var socket = context.WebSocket;
            string uuid = context.QueryString["uuid"].ToString();
            XzyWeChatThread xzy = null;
            DicSocket dicSocket = new DicSocket()
            {
                socket = socket,
                weChatThread = xzy
            };
            if (_dicSockets.ContainsKey(uuid))
            {
                try
                {
                    await _dicSockets[uuid].socket.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//如果client发起close请求，对client进行ack
                }
                catch (Exception ex)
                {
                    LogServer.Info(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "socketErr:" + ex.Message);
                }
            }
            _dicSockets.Add(uuid, dicSocket);
            while (true)
            {
                var buffer = new ArraySegment<byte>(new byte[1024]);
                var receivedResult = await socket.ReceiveAsync(buffer, CancellationToken.None);//对web socket进行异步接收数据
                if (receivedResult.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//如果client发起close请求，对client进行ack
                    }
                    catch (Exception ex)
                    {
                        LogServer.Info(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "socketErr:" + ex.Message);
                    }
                    break;
                }
                if (socket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    string recvMsg = Encoding.UTF8.GetString(buffer.Array, 0, receivedResult.Count);
                    SocketModel model = JsonConvert.DeserializeObject<SocketModel>(recvMsg);
                    switch (model.action.ToLower())
                    {
                        case "start"://创建socket
                            await Task.Factory.StartNew(() =>
                            {
                                xzy = new XzyWeChatThread(socket);
                            });
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("msg/sendtext")]
        public IHttpActionResult SendText(SendTextModel model)
        {
            ApiServerMsg result = new ApiServerMsg();
            try {
                if (_dicSockets.ContainsKey(model.uuid)) {
                    var res=_dicSockets[model.uuid].weChatThread.Wx_SendMsg(model.wxid, model.text);
                    result.Success = true;
                    result.Context = res;
                    return Ok(result);
                }
                else{
                    result.Success = false;
                    result.Context = "不存在该连接";
                    return Ok(result);
                }
              
            } catch (Exception e) {
                result.Success = false;
                result.ErrContext = e.Message;
                return Ok(result);
            }
          
        }
    }
}