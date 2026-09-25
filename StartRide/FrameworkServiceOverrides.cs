using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace StartRide.Core
{
    /// <summary>
    /// 无源码框架层里有几个服务**只能**从 internal 构造函数拿到显式目录：
    /// 它们的 public 构造函数只收 <c>LauncherPathProvider</c>，而那个类是 sealed、
    /// 属性也不可重写，于是目录只能由它按 <c>ApplicationId</c>（框架自己的缩写）现拼。
    ///
    /// 这些 internal 构造函数是稳定存在的（DLL 无源码、不会再变），所以在 DI 注册时
    /// 用反射按签名选中它，把我们自己的目录注进去——框架那套派生路径就永远不会被用到。
    /// </summary>
    public static class FrameworkServiceOverrides
    {
        /// <summary>
        /// 按全名在框架程序集里找类型（这些服务类型本身是 internal，编译期引用不到）。
        /// </summary>
        public static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            Type direct = typeof(Launcher.Infrastructure.LauncherPathProvider).Assembly.GetType(fullName);
            if (direct != null) return direct;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// 在 <paramref name="type"/> 上找一个「第一个参数 HttpClient、第二个参数 string(目录)」
        /// 的构造函数（public/internal 均可）并调用它。找不到返回 null，调用方可退回原注册。
        /// </summary>
        public static object CreateWithExplicitDirectory(Type type, string directory, IServiceProvider serviceProvider)
        {
            if (type == null || string.IsNullOrEmpty(directory)) return null;

            HttpClient http = (serviceProvider?.GetService(typeof(HttpClient)) as HttpClient) ?? new HttpClient();

            // 优先带 IUserFileDeletionService 的重载（行为与框架默认注册最接近）
            object deletionService = serviceProvider?.GetService(Type.GetType(
                "Launcher.Application.Services.IUserFileDeletionService, Launcher.Application", throwOnError: false));

            foreach (ConstructorInfo candidate in type
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderByDescending(c => c.GetParameters().Length))
            {
                ParameterInfo[] ps = candidate.GetParameters();
                if (ps.Length < 2) continue;
                if (ps[0].ParameterType != typeof(HttpClient)) continue;
                if (ps[1].ParameterType != typeof(string)) continue;

                var args = new object[ps.Length];
                args[0] = http;
                args[1] = directory;
                bool ok = true;
                for (int i = 2; i < ps.Length; i++)
                {
                    object value = deletionService != null && ps[i].ParameterType.IsInstanceOfType(deletionService)
                        ? deletionService
                        : serviceProvider?.GetService(ps[i].ParameterType);
                    if (value == null) { ok = false; break; }
                    args[i] = value;
                }
                if (!ok) continue;

                try { return candidate.Invoke(args); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            }
            return null;
        }
    }
}
