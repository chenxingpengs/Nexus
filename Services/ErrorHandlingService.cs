using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nexus.Services
{
    /// <summary>
    /// 错误处理服务 - 提供统一的错误处理和日志记录
    /// </summary>
    public static class ErrorHandlingService
    {
        /// <summary>
        /// 执行异步操作并统一处理错误
        /// </summary>
        public static async Task<(bool Success, T Result, string? ErrorMessage)> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            int maxRetries = 0,
            int retryDelayMs = 1000)
        {
            int attempt = 0;
            Exception lastException = null;

            while (attempt <= maxRetries)
            {
                try
                {
                    var result = await operation();
                    return (true, result, null);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    var errorMsg = GetHttpErrorMessage(ex);
                    LogError(operationName, ex, $"Attempt {attempt + 1}/{maxRetries + 1}");

                    if (attempt < maxRetries && IsRetryableHttpError(ex))
                    {
                        attempt++;
                        await Task.Delay(retryDelayMs * attempt);
                        continue;
                    }

                    return (false, default, errorMsg);
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    LogError(operationName, ex, "JSON parsing error");
                    return (false, default, "服务器返回的数据格式错误，请稍后重试");
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                    LogError(operationName, ex, "Request timeout");
                    return (false, default, "请求超时，请检查网络连接");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogError(operationName, ex, "Unexpected error");
                    return (false, default, $"操作失败：{ex.Message}");
                }
            }

            return (false, default, GetHttpErrorMessage(lastException as HttpRequestException));
        }

        /// <summary>
        /// 执行无返回值的异步操作
        /// </summary>
        public static async Task<(bool Success, string? ErrorMessage)> ExecuteAsync(
            Func<Task> operation,
            string operationName,
            int maxRetries = 0,
            int retryDelayMs = 1000)
        {
            var result = await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, maxRetries, retryDelayMs);

            return (result.Success, result.ErrorMessage);
        }

        /// <summary>
        /// 获取用户友好的 HTTP 错误消息
        /// </summary>
        private static string GetHttpErrorMessage(HttpRequestException ex)
        {
            if (ex == null) return "未知错误";

            var statusCode = ex.StatusCode;
            return statusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => "请求参数错误，请检查输入",
                System.Net.HttpStatusCode.Unauthorized => "未授权，请重新登录",
                System.Net.HttpStatusCode.Forbidden => "没有权限执行此操作",
                System.Net.HttpStatusCode.NotFound => "请求的资源不存在",
                System.Net.HttpStatusCode.InternalServerError => "服务器内部错误，请稍后重试",
                System.Net.HttpStatusCode.BadGateway => "网关错误，请稍后重试",
                System.Net.HttpStatusCode.ServiceUnavailable => "服务暂时不可用，请稍后重试",
                System.Net.HttpStatusCode.GatewayTimeout => "网关超时，请稍后重试",
                null => "无法连接到服务器，请检查：\n1. 后端服务是否已启动\n2. 网络连接是否正常",
                _ => $"请求失败：{ex.Message}"
            };
        }

        /// <summary>
        /// 判断 HTTP 错误是否可重试
        /// </summary>
        private static bool IsRetryableHttpError(HttpRequestException ex)
        {
            if (ex?.StatusCode == null) return true;

            return ex.StatusCode switch
            {
                System.Net.HttpStatusCode.InternalServerError => true,
                System.Net.HttpStatusCode.BadGateway => true,
                System.Net.HttpStatusCode.ServiceUnavailable => true,
                System.Net.HttpStatusCode.GatewayTimeout => true,
                System.Net.HttpStatusCode.RequestTimeout => true,
                _ => false
            };
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private static void LogError(string operationName, Exception ex, string context)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var separator = new string('=', 80);

            // 构建详细的错误信息
            var logMessage = $"{separator}\n" +
                           $"[{timestamp}] [{operationName}] {context}\n" +
                           $"{separator}\n" +
                           $"Exception Type: {ex.GetType().FullName}\n" +
                           $"Message: {ex.Message}\n" +
                           $"{separator}\n" +
                           $"STACK TRACE:\n{ex.StackTrace}\n" +
                           $"{separator}";

            // 输出到 VS 调试输出窗口
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine(logMessage);
            System.Diagnostics.Debug.WriteLine("");

            // 同时输出到控制台
            Console.WriteLine(logMessage);

            // 如果有内部异常，也输出
            if (ex.InnerException != null)
            {
                var innerMessage = $"{separator}\n" +
                                 $"INNER EXCEPTION:\n" +
                                 $"Type: {ex.InnerException.GetType().FullName}\n" +
                                 $"Message: {ex.InnerException.Message}\n" +
                                 $"StackTrace: {ex.InnerException.StackTrace}\n" +
                                 $"{separator}";

                System.Diagnostics.Debug.WriteLine(innerMessage);
                Console.WriteLine(innerMessage);
            }
        }

        /// <summary>
        /// 验证参数不为空
        /// </summary>
        public static void ValidateNotNull(object value, string paramName)
        {
            if (value == null)
                throw new ArgumentNullException(paramName, $"{paramName} 不能为空");
        }

        /// <summary>
        /// 验证字符串不为空或空白
        /// </summary>
        public static void ValidateNotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName} 不能为空或空白", paramName);
        }
    }
}
