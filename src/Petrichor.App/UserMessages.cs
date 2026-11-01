using System;
using Petrichor.Core;
namespace Petrichor.App
{
    internal static class UserMessages
    {
        public static string For(Exception error)
        {
            var apiError = error as MusicApiException;
            if (apiError == null) return "暂时无法完成操作，请重试。";
            if (apiError.RequiresLogin) return "请重新登录后再试。";
            switch (apiError.Code)
            {
                case 460: return "这首歌目前只能试听，无法下载完整歌曲。";
    case 403: return "暂时无法完成操作，请稍后再试。";
                case 502: return "音乐服务暂时不可用，请稍后再试。";
                case 503: return "连接失败，请检查网络后重试。";
                case 504: return "连接超时，请稍后重试。";
                default: return "暂时无法完成操作，请稍后重试。";
            }
        }
    }
}

