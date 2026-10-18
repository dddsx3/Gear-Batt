using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GearBatt
{
    internal sealed class CollectorForm : Form
    {
        private readonly CheckBox _consent;
        private readonly TextBox _model;
        private readonly Button _collect;
        private readonly Label _status;
        private readonly TextBox _result;
        private readonly Button _openReport;
        private readonly Button _openFolder;
        private readonly Button _copyPath;
        private readonly Button _github;
        private readonly Button _exit;

        private bool _busy;
        private string _reportPath = "";
        private readonly float _scale;

        public CollectorForm()
        {
            using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
                _scale = screen.DpiX / 96f;
            if (_scale <= 0.5f || _scale > 4f)
                _scale = 1f;

            Text = "Gear-Batt 数据采集器（自愿参加）";
            ClientSize = new Size(S(660), S(630));
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;

            Label title = new Label();
            title.Text = "Gear-Batt 兼容性数据采集器";
            title.Font = new Font(Font.FontFamily, 15f, FontStyle.Bold);
            Place(title, 20, 14, 620, 32);
            Controls.Add(title);

            TextBox notice = new TextBox();
            notice.Multiline = true;
            notice.ReadOnly = true;
            notice.ScrollBars = ScrollBars.Vertical;
            notice.BorderStyle = BorderStyle.FixedSingle;
            notice.BackColor = SystemColors.Control;
            notice.TabStop = false;
            notice.Text =
                "这是完全自愿的兼容性改进工具，不是使用 Gear-Batt 的前提。\r\n\r\n" +
                "它只读取设备的技术信息：VID/PID、HID 使用页/用途、解析后的 HID 字段信息、" +
                "只读电量协议响应，以及 Windows/蓝牙电量值。\r\n\r\n" +
                "它不会读取按键、鼠标移动、原始输入报告、用户名、计算机名、IP、" +
                "序列号或蓝牙地址；不会联网，也不会自动上传。\r\n\r\n" +
                "报告中会包含设备名称/型号和 VID/PID；原始协议响应中也可能包含设备侧型号/ID。\r\n" +
                "如果这些内容包含个人信息，请在分享前修改。\r\n\r\n" +
                "报告只保存在本机。你可以先打开报告检查内容，再决定是否发送到 GitHub Issue。";
            Place(notice, 20, 52, 620, 160);
            Controls.Add(notice);

            _consent = new CheckBox();
            _consent.Text = "我已阅读以上说明，并自愿提供这些技术数据。";
            Place(_consent, 20, 220, 620, 26);
            _consent.CheckedChanged += delegate { UpdateButtons(); };
            Controls.Add(_consent);

            Label modelLabel = new Label();
            modelLabel.Text = "设备型号（可选，例如：罗技 G304）";
            Place(modelLabel, 20, 254, 220, 24);
            Controls.Add(modelLabel);

            _model = new TextBox();
            Place(_model, 245, 252, 395, 24);
            _model.MaxLength = 100;
            Controls.Add(_model);

            _collect = new Button();
            _collect.Text = "采集并保存报告";
            Place(_collect, 20, 288, 180, 34);
            _collect.Click += OnCollect;
            Controls.Add(_collect);

            _status = new Label();
            _status.Text = "等待开始";
            Place(_status, 215, 296, 425, 22);
            Controls.Add(_status);

            _result = new TextBox();
            _result.Multiline = true;
            _result.ReadOnly = true;
            _result.ScrollBars = ScrollBars.Vertical;
            _result.BorderStyle = BorderStyle.FixedSingle;
            _result.BackColor = SystemColors.Window;
            Place(_result, 20, 334, 620, 178);
            _result.Text = "报告会生成在本机，不会自动上传。";
            Controls.Add(_result);

            _openReport = new Button();
            _openReport.Text = "打开报告";
            Place(_openReport, 20, 526, 130, 32);
            _openReport.Click += delegate { OpenReport(); };
            Controls.Add(_openReport);

            _openFolder = new Button();
            _openFolder.Text = "打开所在文件夹";
            Place(_openFolder, 160, 526, 130, 32);
            _openFolder.Click += delegate { OpenFolder(); };
            Controls.Add(_openFolder);

            _copyPath = new Button();
            _copyPath.Text = "复制路径";
            Place(_copyPath, 300, 526, 100, 32);
            _copyPath.Click += delegate { CopyPath(); };
            Controls.Add(_copyPath);

            _github = new Button();
            _github.Text = "打开 GitHub Issue";
            Place(_github, 410, 526, 150, 32);
            _github.Click += delegate { OpenIssuePage(); };
            Controls.Add(_github);

            _exit = new Button();
            _exit.Text = "退出";
            Place(_exit, 570, 526, 70, 32);
            _exit.Click += delegate { Close(); };
            Controls.Add(_exit);

            Label footer = new Label();
            footer.Text = "本程序不联网；只有你点击“打开 GitHub Issue”时才会启动浏览器。请手动附加报告。";
            footer.ForeColor = SystemColors.GrayText;
            Place(footer, 20, 570, 620, 42);
            Controls.Add(footer);

            UpdateButtons();
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private void Place(Control control, int x, int y, int width, int height)
        {
            control.SetBounds(S(x), S(y), S(width), S(height));
        }

        private void OnCollect(object sender, EventArgs e)
        {
            if (_busy || !_consent.Checked)
                return;

            _busy = true;
            _consent.Enabled = false;
            _model.Enabled = false;
            _status.Text = "正在采集…";
            _result.Text = "正在扫描 HID 接口并执行只读电量查询，请稍候…\r\n" +
                "这通常需要几秒钟。";
            UpdateButtons();

            string model = _model.Text.Trim();
            ThreadPool.QueueUserWorkItem(delegate
            {
                CollectorResult result = null;
                Exception failure = null;
                try
                {
                    result = CollectorEngine.Collect(model, null, true);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        OnCollected(result, failure);
                    }));
                }
                catch (Exception)
                {
                }
            });
        }

        private void OnCollected(CollectorResult result, Exception failure)
        {
            _busy = false;
            _consent.Enabled = true;
            _model.Enabled = true;

            if (failure != null)
            {
                _status.Text = "采集失败";
                _result.Text = "采集失败：\r\n" + failure.Message +
                    "\r\n\r\n你可以重试；如果仍然失败，请在 GitHub Issue 中描述错误。";
                UpdateButtons();
                return;
            }

            if (result == null)
            {
                _status.Text = "采集失败";
                _result.Text = "采集失败：没有生成结果。";
                UpdateButtons();
                return;
            }

            _reportPath = result.ReportPath;
            _status.Text = "采集完成";
            _result.Text = result.Summary +
                "\r\n\r\n报告已保存到：\r\n" + _reportPath +
                "\r\n\r\n请先打开报告检查内容，再决定是否分享。";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _collect.Enabled = !_busy && _consent.Checked;
            bool hasReport = !_busy && !string.IsNullOrEmpty(_reportPath) &&
                File.Exists(_reportPath);
            _openReport.Enabled = hasReport;
            _openFolder.Enabled = hasReport;
            _copyPath.Enabled = hasReport;
            _github.Enabled = !_busy;
        }

        private void OpenReport()
        {
            if (!string.IsNullOrEmpty(_reportPath) && File.Exists(_reportPath))
            {
                try
                {
                    Process.Start(_reportPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "无法打开报告：\n" + ex.Message,
                        "Gear-Batt 数据采集器",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(_reportPath))
                return;
            try
            {
                Process.Start("explorer.exe",
                    "/select,\"" + _reportPath + "\"");
            }
            catch (Exception)
            {
            }
        }

        private void CopyPath()
        {
            if (string.IsNullOrEmpty(_reportPath))
                return;
            try
            {
                Clipboard.SetText(_reportPath);
                _status.Text = "报告路径已复制";
            }
            catch (Exception)
            {
            }
        }

        private void OpenIssuePage()
        {
            try
            {
                Process.Start("https://github.com/dddsx3/Gear-Batt/issues/new");
                _status.Text = "浏览器已打开；请手动附加报告";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "无法打开浏览器：\n" + ex.Message +
                    "\n\n请手动访问：\nhttps://github.com/dddsx3/Gear-Batt/issues/new",
                    "Gear-Batt 数据采集器",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
