using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using DNS.Server;
using System.Net;

namespace LeiKaiFengService
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



    public partial class Service1 : ServiceBase
    {
        DnsServer _server;

        MasterFile GetMasterFile()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;


            var hostsPath = Path.Combine(basePath, "hosts.txt");

            if (File.Exists(hostsPath) == false)
            {
                return new MasterFile();
            }

            string s = File.ReadAllText(hostsPath);

            var mf = new MasterFile();

            foreach (var item in ParseHosts.CreateMasterFile(s))
            {
                mf.AddIPAddressResourceRecord(item.Key, item.Value.ToString());
            }

            return mf;
        }

        public Service1()
        {
            InitializeComponent();

            _server = new DnsServer(GetMasterFile(), "114.114.114.114");
        }
        protected override void OnStart(string[] args)
        {
            _server.Listen();
        }
        protected override void OnStop()
        {
            _server.Dispose();
        }
        
    }
}
