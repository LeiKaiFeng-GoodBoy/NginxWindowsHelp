using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using LeiKaiFeng.X509Certificates;
using Microsoft.Win32.TaskScheduler;

namespace SslCertCreate
{

    static class ParseHosts
    {
        static string[] RemoveComment(string s)
        {
            return s.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where((item) => item.StartsWith("#") == false)
                .Where((item) => string.IsNullOrWhiteSpace(item) == false)
                .ToArray();
        }

        static void CheckHost(string s)
        {

            if (s.StartsWith("*"))
            {
                s = "www" + s.Remove(0, 1);
            }


            if (System.Uri.CheckHostName(s) == UriHostNameType.Dns)
            {

            }
            else
            {
                throw new UriFormatException();
            }
        }

        public static Dictionary<string, IPAddress> CreateMasterFile(string s)
        {

            var dic = new Dictionary<string, IPAddress>();

            foreach (var item in RemoveComment(s))
            {
                var kv = item.Split(new string[] { " " }, 3, StringSplitOptions.RemoveEmptyEntries);




                try
                {
                    if (kv.Length < 2)
                    {
                        throw new FormatException();
                    }


                    CheckHost(kv[1]);



                    dic.Add(kv[1], IPAddress.Parse(kv[0]));

                }
                catch (FormatException)
                {
                    //throw new FormatException($"{item} 这一项存在错误");
                }
            }

            return dic;
        }

    }


    class Program
    {
        
        static string GetDnsProxyServerEXEPath()
        {
            
            var path = Path.Combine(GetDNSProxyFolderPath(), "LeiKaiFengService.exe");

            return path;
        }

        static void JumInstall(System.Action action)
        {
            while (true)
            {
                Console.WriteLine("1.继续安装");
                Console.WriteLine("2.跳过安装");
                Console.WriteLine("请选择");
                string s = Console.ReadLine();

                if (s == "1")
                {
                    action();

                    return;
                }
                else if(s == "2")
                {
                    return;
                }
            }       
        }

        static void InstallSrver()
        {

            





            Process
                .Start(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe", GetDnsProxyServerEXEPath())
                .WaitForExit();
        }

        static void RunBat()
        {
            Process.Start(GetBatPath()).WaitForExit();
        }

        static string GetStopBat()
        {
            return Path.Combine(GetNginxFolderPath(), "stop.bat");
        }

        static void RunStopBat()
        {
            Process.Start(GetStopBat()).WaitForExit();
        }

        static void UninstallServer()
        {


            Process.Start(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe", "-u " + GetDnsProxyServerEXEPath()).WaitForExit();
        }

        static string GetNginxFolderPath()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            var dirPath = Path.Combine(basePath, "nginx");

            return dirPath;
        }

        static string GetBatPath()
        {
           
            var filePath = Path.Combine(GetNginxFolderPath(), "start.bat");


            return filePath;
        }

        static void CreateStartBat()
        {
            
            

            File.WriteAllText(GetBatPath(), $"ipconfig /flushdns && cd {GetNginxFolderPath()} && nginx.exe");
        }

        static NetworkInterface GetActiveEthernetOrWifiNetworkInterface()
        {
            var Nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString() == "InterNetwork"));

            return Nic;
        }

        static void SetDNS(string DnsString, string beiyongDns)
        {
            string[] Dns = { DnsString, beiyongDns };
            var currentInterface = GetActiveEthernetOrWifiNetworkInterface();
            if (currentInterface == null) return;

            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();
            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    if (objMO["Description"].ToString().Equals(currentInterface.Description))
                    {
                        ManagementBaseObject objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                        if (objdns != null)
                        {
                            objdns["DNSServerSearchOrder"] = Dns;
                            objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                        }
                    }
                }
            }
        }

        const string TASKNAME = "nginx login start task";

        static void UnInstallTask()
        {
            try
            {

                // Get the service on the local machine
                using (TaskService ts = new TaskService())
                {

                    // Remove the task we just created
                    ts.RootFolder.DeleteTask(TASKNAME);
                }
            }
            catch (FileNotFoundException)
            {

            }

        }

        static void InstallTask()
        {
            // Get the service on the local machine
            using (TaskService ts = new TaskService())
            {
                // Create a new task definition and assign properties
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "login start nginx";

                // Create a trigger that will fire the task at this time every other day
                td.Triggers.Add(new  LogonTrigger());

                // Create an action that will launch Notepad whenever the trigger fires
                td.Actions.Add(new ExecAction(GetBatPath()));

                // Register the task in the root folder
                ts.RootFolder.RegisterTaskDefinition(TASKNAME, td);

                // Remove the task we just created
                //ts.RootFolder.DeleteTask("Test");
            }
        }

        static string GetDNSProxyFolderPath()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(basePath, "DNSProxy");
        }

        static string GetNginxConfigFolderPath()
        {
            
            return Path.Combine(GetNginxFolderPath(), "conf");
        }

        static string[] GetSubName()
        {
            
            var hostsPath = Path.Combine(GetDNSProxyFolderPath(), "hosts.txt");


            return ParseHosts.CreateMasterFile(File.ReadAllText(hostsPath))
                .Select((item) => item.Key).ToArray();

        }

        const string CANAME = "LeiKaiFeng nginx CA";

        static void RemoveCA()
        {
            using (X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {

                store.Open(OpenFlags.ReadWrite);

                foreach (var item in store.Certificates)
                {
                    if (item.Subject.IndexOf(CANAME) != -1)
                    {
                        store.Remove(item);
                    }
                }
            }
        }

        static void CreateCert()
        {
            var ca = TLSBouncyCastleHelper.GenerateCA(CANAME, 2048, 3000);


            var cert = TLSBouncyCastleHelper.GenerateTls(
                CaPack.Create(ca),
                "Leikaifng iwara.tv",
                2048,
                3000,
                GetSubName());


            var pemCert = TLSBouncyCastleHelper.CreatePem.AsPem(cert);


            var pemKey = TLSBouncyCastleHelper.CreatePem.AsKey(cert);


            using (X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {

                store.Open(OpenFlags.ReadWrite);

                store.Add(new X509Certificate2(ca.Export(X509ContentType.Cert)));
            }


            var basePath = GetNginxConfigFolderPath();


            Directory.CreateDirectory(basePath);



            var pemPath = Path.Combine(basePath, "cert.pem");
            var cerPath = Path.Combine(basePath, "cert.cer");
            var keyPath = Path.Combine(basePath, "cert.key");
            var caPath = Path.Combine(basePath, "ca.cer");

            File.WriteAllBytes(cerPath, cert.Export(X509ContentType.Cert));

            File.WriteAllBytes(pemPath, pemCert);

            File.WriteAllBytes(keyPath, pemKey);

            File.WriteAllBytes(caPath, ca.Export(X509ContentType.Cert));
        }


        static void Install()
        {
            

            Console.WriteLine("正在安装证书，请在弹出的窗口中选择是");
            CreateCert();

            Console.WriteLine("正在安装DNSProxy服务");
            JumInstall(() => InstallSrver());

            Console.WriteLine("正在准备设置DNS服务器设置，请保持最常用的网络链接，按回车继续");
            Console.ReadLine();
            SetDNS("127.0.0.1", "8.8.8.8");


            Console.WriteLine("正在生成bat脚本");
            CreateStartBat();

            Console.WriteLine("正在注册Windows计划任务");
            InstallTask();


            Console.WriteLine("正在启动bat脚本， 可以手动把新启动的窗口关掉");
            RunStopBat();
            RunBat();

            Console.WriteLine("安装完毕，回车退出");

            Console.ReadLine();
        }

        static void Uninstall()
        {
            Console.WriteLine("正在删除证书，请在弹出的窗口中选择是，可能不止弹一次");
            RemoveCA();

            Console.WriteLine("正在卸载DNSProxy服务");
            UninstallServer();

            Console.WriteLine("正在准备设置DNS服务器设置");

            SetDNS("8.8.8.8", "1.1.1.1");


            
            Console.WriteLine("正在删除Windows计划任务");
            UnInstallTask();


            Console.WriteLine("正在关闭nginx");

            RunStopBat();

            Console.WriteLine("卸载完毕，回车退出");

            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            
            while (true)
            {
                Console.WriteLine("执行过程可能会自动弹出窗口，除非提示可以关闭否则请等待自动关闭");
                Console.WriteLine("可能要等一会，不是卡住了");
                Console.WriteLine("1.安装");
                Console.WriteLine("2.卸载");
                Console.WriteLine("请要执行的操作");

                string s = Console.ReadLine();

                if (s == "1")
                {
                    Install();

                    return;
                }
                else if(s == "2")
                {
                    Uninstall();

                    return;
                }
                
            }
           
        }
    }
}
