using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net;

namespace _4RTools.Model
{
    public class JsObject
    {
        public string execName
        {
            get; set;
        }

        public string ServerDesc
        {
            get; set;
        }

        public int currentNameAddress
        {
            get; set;
        }

        public int currentHPBaseAddress
        {
            get; set;
        }

        public int statusBufferAddress
        {
            get; set;
        }




        private JsObject(string execName, string serverdesc, int currentHPBaseAddress, int currentNameAddress)
        {
            this.currentNameAddress = currentNameAddress;
            this.ServerDesc = serverdesc;
            this.currentHPBaseAddress = currentHPBaseAddress;
            this.execName = execName;
            this.statusBufferAddress = currentHPBaseAddress + 0x474;
        }
    }
    public sealed class ClientSingleton
    {
        private static Client client;
        private ClientSingleton(Client client)
        {
            ClientSingleton.client = client;
        }

        public static ClientSingleton Instance(Client client)
        {
            return new ClientSingleton(client);
        }

        public static Client GetClient()
        {
            return client;
        }
    }

    public class Client
    {
        public Process process { get; }

        private static int MAX_POSSIBLE_HP = 1000000;
        private string execName { get; set; }
        private Utils.ProcessMemoryReader PMR { get; set; }
        private int currentNameAddress { get; set; }
        private int currentHPBaseAddress { get; set; }
        private int statusBufferAddress { get; set; }
        private int _num = 0;

        private Client(string execName, int currentHPBaseAddress, int currentNameAddress)
        {
            this.currentNameAddress = currentNameAddress;
            this.currentHPBaseAddress = currentHPBaseAddress;
            this.execName = execName;
            this.statusBufferAddress = currentHPBaseAddress + 0x474;
        }


        public Client(string processName)
        {
            PMR = new Utils.ProcessMemoryReader();
            string rawProcessName = processName.Split(new string[] { ".exe - " }, StringSplitOptions.None)[0];
            int choosenPID = int.Parse(processName.Split(new string[] { ".exe - " }, StringSplitOptions.None)[1]);

            foreach (Process process in Process.GetProcessesByName(rawProcessName))
            {
                if (choosenPID == process.Id)
                {
                    this.process = process;
                    PMR.ReadProcess = process;
                    PMR.OpenProcess();

                    try
                    {
                        Client c = GetClientByProcess(rawProcessName);

                        if (c == null) throw new Exception();

                        this.currentHPBaseAddress = c.currentHPBaseAddress;
                        this.currentNameAddress = c.currentNameAddress;
                        this.statusBufferAddress = c.statusBufferAddress;
                    } catch
                    {
                        MessageBox.Show("This client is not supported. Only Spammers and macro will works.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        this.currentHPBaseAddress = 0;
                        this.currentNameAddress = 0;
                        this.statusBufferAddress = 0;
                    }

                    //Do not block spammer for non supported Versions

                }
            }
        }

        private string ReadMemoryAsString(int address)
        {
            byte[] bytes = PMR.ReadProcessMemory((IntPtr)address, 40u, out _num);
            List<byte> buffer = new List<byte>(); //Need a list with dynamic size 
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0) break; //Check Nullability based ON ASCII Table

                buffer.Add(bytes[i]); //Add only bytes needed
            }

            return Encoding.Default.GetString(buffer.ToArray());

        }

        private uint ReadMemory(int address)
        {
            return BitConverter.ToUInt32(PMR.ReadProcessMemory((IntPtr)address, 4u, out _num), 0);
        }
        public void WriteMemory(int address, uint intToWrite)
        {
            PMR.WriteProcessMemory((IntPtr)address, BitConverter.GetBytes(intToWrite), out _num);
        }

        public void WriteMemory(int address, byte[] bytesToWrite)
        {
            PMR.WriteProcessMemory((IntPtr)address, bytesToWrite, out _num);
        }

        public bool IsHpBelow(int percent)
        {
            return ReadCurrentHp() * 100 < percent * ReadMaxHp();
        }

        public bool IsSpBelow(int percent)
        {
            return ReadCurrentSp() * 100 < percent * ReadMaxSp();
        }

        public string HpLabel()
        {
            return string.Format("{0} / {1}", ReadCurrentHp(), ReadMaxHp());
        }

        public string SpLabel()
        {
            return string.Format("{0} / {1}", ReadCurrentSp(), ReadMaxSp());
        }

        public uint ReadCurrentHp()
        {
            return ReadMemory(this.currentHPBaseAddress);
        }

        public uint ReadCurrentSp()
        {
            return ReadMemory(this.currentHPBaseAddress + 8);
        }

        public uint ReadMaxHp()
        {
            return ReadMemory(this.currentHPBaseAddress + 4);
        }

        public string ReadCharacterName()
        {
            return ReadMemoryAsString(this.currentNameAddress);
        }

        public uint ReadMaxSp()
        {
            return ReadMemory(this.currentHPBaseAddress + 12);
        }

        public uint CurrentBuffStatusCode(int effectStatusIndex)
        {
            return ReadMemory(this.statusBufferAddress + effectStatusIndex * 4);
        }

        public Client GetClientByProcess(string processName)
        {

            foreach (Client c in GetAll())
            {
                if (c.execName == processName)
                {

                    uint hpBaseValue = ReadMemory(c.currentHPBaseAddress);
                    uint hpMaxValue = ReadMemory(c.currentHPBaseAddress + 4);
                    uint spBaseValue = ReadMemory(c.currentHPBaseAddress + 8);
                    uint spMaxValue = ReadMemory(c.currentHPBaseAddress + 12);
                    string namae = ReadCharacterName();
                    MessageBox.Show("HP:" + hpBaseValue + "/" + hpMaxValue + ".- SP:" + spBaseValue + "/" + spMaxValue + "\n"+"character name:"+ namae, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (hpBaseValue > 0 && hpBaseValue < MAX_POSSIBLE_HP) return c;
                }
            }
            return null;
        }
        private static List<string> GetSources(bool remote)//true remote , false local
        {
            List<string> sources = new List<string>();
            string jsonFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            string jsonstr = System.IO.File.ReadAllText(jsonFilePath);
            if (remote)
            {
                JObject obj = JObject.Parse(jsonstr);
                //MessageBox.Show("remote first:" + obj["Remote"].First.Value<string>(), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                sources.AddRange(obj["Remote"].Values<string>());
            } else
            {
                JObject obj = JObject.Parse(jsonstr);
                //MessageBox.Show("local first:" + obj["Local"].First.Value<string>(), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                sources.AddRange(obj["Local"].Values<string>());
            }
            return sources;
        }

        private static List<Client> GetLocal()
        {
            List<Client> list = new List<Client>();
            foreach (string source in GetSources(false))
            {
                string jsonFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, source);
                string jsonstr = System.IO.File.ReadAllText(jsonFilePath);
                var jarr = JArray.Parse(jsonstr);
                if (jarr.Count == 0)
                {
                    MessageBox.Show("local server list empty , missing or failed to read", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                foreach (JToken obj2 in jarr)
                {
                    list.Add(new Client(obj2.Value<string>(key: "name"), Convert.ToInt32(obj2.Value<String>(key: "hpAddress"), 16), Convert.ToInt32(obj2.Value<String>(key: "nameAddress"), 16)));
                }
            }
            //MessageBox.Show("found:" + list.Count.ToString() + "Local supported Servers.-", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return list;
        }
        private static List<Client> GetRemote()
        {
            List<Client> list = new List<Client>();
            foreach (string source in GetSources(true))
            {

                string jsonurl = System.IO.Path.Combine(source);
                string json = "";
                try
                {
                    Uri myUri = new Uri(source);
                    using (WebClient web = new WebClient())
                    {
                        json = web.DownloadString(myUri);
                    }
                }
                catch(Exception ex)
                {
                    //MessageBox.Show(ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                }

                var jarr = JArray.Parse(json);
                if (jarr.Count == 0)
                {
                    //MessageBox.Show("local server list empty , missing or failed to read", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                foreach (JToken obj2 in jarr)
                {
                    list.Add(new Client(obj2.Value<string>(key: "name"), Convert.ToInt32(obj2.Value<String>(key: "hpAddress"), 16), Convert.ToInt32(obj2.Value<String>(key: "nameAddress"), 16)));
                }
            }
            MessageBox.Show("found:"+list.Count.ToString() + "Remote supported Servers.-", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return list;
        }
         private static List<Client> GetAll() {
                List<Client> client = new List<Client>();
                try
                {
                    client.AddRange(GetLocal());
                }
                catch
                {
                    //MessageBox.Show("local client list errored", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                try
                {
                    client.AddRange(GetRemote());
                }
                catch
                {
                    //MessageBox.Show("remote client list errored", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                if (client.Count == 0)
                {
                    MessageBox.Show("server list empty", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return client;
            }

        }
    } 

