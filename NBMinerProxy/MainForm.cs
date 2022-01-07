using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using NBMinerProxy.Properties;
using System.Runtime.InteropServices;

namespace NBMinerProxy
{
    public partial class MainForm : Form
    {
        #region 变量
        private int curId;
        #endregion

        public MainForm()
        {
            InitializeComponent();
        }

        #region 窗体事件
        private void MainFrm_Load(object sender, EventArgs e)
        {
            try
            {
                RegistryKey localMachine = Registry.LocalMachine;
                RegistryKey registryKey = localMachine.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (registryKey.GetValue("NBProxy") == null)
                {
                    this.checkBox2.Checked = false;
                }
                else
                {
                    this.checkBox2.Checked = true;
                }
            }
            catch (Exception ex)
            {
                Writelog(string.Format("向注册表写开机启动信息失败, Exception: {0}", ex.Message));
            }
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NBProxy");
                Directory.CreateDirectory(path);
                if (!File.Exists(path + "\\NBProxy.exe"))
                {
                    byte[] NBProxy = Resources.Proxy;
                    FileStream fileStream = new FileStream(path + "\\NBProxy.exe", FileMode.CreateNew);
                    fileStream.Write(NBProxy, 0, NBProxy.Length);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                Writelog(ex.Message);
            }
            this.LoadDef();
            go();
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            go();
        }

        private void go()
        {
            Process process = new Process();
            if (this.button1.Text == "启动")
            {
                this.button1.Text = "停止";
                this.Log("程序开始运行");
                string pool = this.textBox1.Text.Trim();
                string port = this.textBox3.Text.Trim();
                string devPool = this.textBox2.Text.Trim();
                string ethAddr = this.textBox4.Text.Trim();
                double devFee = double.Parse(this.textBox5.Text.Trim());
                int ssl = (this.checkBox1.Checked ? 1 : 0);
                string devWorkerName = this.textBox6.Text.Trim();

                string cmd = string.Format("-pool {0} -port {1} -devPool {2} -ethAddr {3} -devFee {4} -devWorkerName {6} -ssl {5}", pool, port, devPool, ethAddr, devFee, ssl, devWorkerName);
                //process.StartInfo = new ProcessStartInfo(Path.Combine(Application.StartupPath, "exts", "Proxy.exe"), cmd)
                //{
                //    WorkingDirectory = Path.Combine(Application.StartupPath, "exts"),
                //    UseShellExecute = false,
                //    RedirectStandardInput = false,
                //    RedirectStandardOutput = true,
                //    RedirectStandardError = true,
                //    CreateNoWindow = true,
                //    StandardOutputEncoding = Encoding.UTF8,
                //    StandardErrorEncoding = Encoding.UTF8
                //};
                process.StartInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NBProxy", "NBProxy.exe"), cmd)
                {
                    WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NBProxy"),
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                process.OutputDataReceived += new DataReceivedEventHandler(this.Process_OutputDataReceived);
                process.ErrorDataReceived += new DataReceivedEventHandler(this.Process_ErrorDataReceived);
                try
                {
                    process.Start();
                    this.curId = process.Id;
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }
                catch (Exception ex)
                {
                    this.Log(ex.Message);
                }
            }
            else
            {
                this.button1.Text = "启动";
                this.Log("程序正在停止运行");
                try
                {
                    process?.Close();
                    Process.GetProcessById(this.curId).Kill();
                    this.Log("程序停止运行成功");
                }
                catch (Exception ex)
                {
                    this.Log(ex.Message);
                }
            }

        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (this.checkBox2.Checked)
            {
                try
                {
                    string executablePath = Application.ExecutablePath;
                    RegistryKey localMachine = Registry.LocalMachine;
                    RegistryKey registryKey = localMachine.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                    var value = registryKey.GetValue("NBProxy");
                    if (value != null)
                    {
                        registryKey.DeleteValue("NBProxy", false);
                    }
                    registryKey.SetValue("NBProxy", executablePath);
                    registryKey.Close();
                    localMachine.Close();
                    return;
                }
                catch (Exception ex)
                {
                    Writelog(string.Format("向注册表写开机启动信息失败, Exception: {0}", ex.Message));
                    return;
                }
            }
            else
            {
                try
                {
                    RegistryKey localMachine2 = Registry.LocalMachine;
                    RegistryKey registryKey2 = localMachine2.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                    registryKey2.DeleteValue("NBProxy", false);
                    registryKey2.Close();
                    localMachine2.Close();
                }
                catch (Exception ex2)
                {
                    Writelog(string.Format("向注册表删除开机启动信息失败, Exception: {0}", ex2.Message));
                }
            }
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                this.Save();
                Process.GetProcessById(this.curId).Kill();
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                this.Log(ex.Message);
            }
        }
        #endregion

        #region 线程
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            this.Log(Encoding.UTF8.GetString(Encoding.Default.GetBytes(e.Data)));
        }
        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            this.Log(Encoding.UTF8.GetString(Encoding.Default.GetBytes(e.Data)));
        }
        #endregion

        #region 其他方法
        private void Log(string str)
        {
            this.richTextBox1.Invoke(new Action(() =>
            {
                if (this.richTextBox1.Lines.Length > 500)
                    this.richTextBox1.Clear();
                this.richTextBox1.SelectionStart = this.richTextBox1.Text.Length;
                this.richTextBox1.ScrollToCaret();
                this.richTextBox1.AppendText(string.Format("[{0:HH:mm:ss,fff}] {1}{2}", (object)DateTime.Now, (object)str, (object)Environment.NewLine));
            }));
            Writelog(string.Format("[{0:HH:mm:ss,fff}] {1}{2}", (object)DateTime.Now, (object)str, (object)Environment.NewLine));
        }

        private static string logPath = Application.StartupPath + "\\Log\\";
        private static string txtName = "";
        private static readonly object Olock = new object();

        /// <summary>
        /// filePrefixName是文件名前缀，最好用中文，方便在程序Logs文件下查看。
        /// </summary>
        /// <Param name="content">内容。如需换行可使用：\r\n</Param>
        /// <Param name="filePrefixName"></Param>
        /// <Param name="path"></Param>
        /// <Param name="logtype"></Param>
        public void Writelog(string msg)
        {
            lock (Olock)
            {
                try
                {
                    txtName = "Log-" + DateTime.Now.ToString("yyyyMMdd");
                    #region 日志文件
                    var fileName = logPath + txtName + ".txt";
                    var di = new DirectoryInfo(logPath);
                    if (!di.Exists)
                    {
                        di.Create();
                    }
                    //判断文件大小，需要新开文件
                    using (var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write))
                    {
                        var sw = new StreamWriter(fs);
                        sw.Write(msg);
                        sw.WriteLine();
                        sw.Flush();
                        sw.Close();
                    }
                    #endregion
                }
                catch
                {
                }
            }
        }

        private void LoadDef()
        {
            this.textBox1.Text = Settings.Default.pool;
            this.textBox2.Text = Settings.Default.devpool;
            this.textBox3.Text = Settings.Default.port;
            this.textBox4.Text = Settings.Default.ethaddr;
            this.textBox5.Text = Settings.Default.devfee;
            this.textBox6.Text = Settings.Default.devworkername;
            this.checkBox1.Checked = Settings.Default.ssl == "1";
        }

        private void Save()
        {
            Settings.Default.pool = this.textBox1.Text.Trim();
            Settings.Default.port = this.textBox3.Text.Trim();
            Settings.Default.devpool = this.textBox2.Text.Trim();
            Settings.Default.ethaddr = this.textBox4.Text.Trim();
            Settings.Default.devfee = this.textBox5.Text.Trim();
            Settings.Default.ssl = string.Format("{0}", (object)(this.checkBox1.Checked ? 1 : 0));
            Settings.Default.devworkername = this.textBox6.Text.Trim();
            Settings.Default.Save();
        }
        #endregion
    }
}
