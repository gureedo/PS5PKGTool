#nullable enable

namespace PS5PKGTool.Forms;

partial class TextReportForm
{
    private System.ComponentModel.IContainer? components = null;
    private DarkUI.Controls.DarkRichTextBox txtReport = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TextReportForm));
        txtReport = new DarkUI.Controls.DarkRichTextBox();
        SuspendLayout();
        // 
        // txtReport
        // 
        txtReport.DetectUrls = false;
        txtReport.Dock = DockStyle.Fill;
        txtReport.Font = new Font("Consolas", 9F);
        txtReport.Location = new Point(0, 0);
        txtReport.Name = "txtReport";
        txtReport.ReadOnly = true;
        txtReport.SelectionColor = Color.Gainsboro;
        txtReport.SelectionFont = new Font("Consolas", 9F);
        txtReport.Size = new Size(760, 460);
        txtReport.TabIndex = 0;
        txtReport.TextPadding = new Padding(3);
        txtReport.WordWrap = false;
        // 
        // TextReportForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(60, 63, 65);
        ClientSize = new Size(760, 460);
        Controls.Add(txtReport);
        MinimizeBox = false;
        MinimumSize = new Size(520, 320);
        Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
        Name = "TextReportForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "PS5 PKG Tool";
        ResumeLayout(false);
    }
}
