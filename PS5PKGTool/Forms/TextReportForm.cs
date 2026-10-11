using DarkUI.Forms;
using System.Drawing;
using System.Windows.Forms;

namespace PS5PKGTool.Forms;

public partial class TextReportForm : DarkForm
{
    public TextReportForm()
    {
        InitializeComponent();
    }

    public TextReportForm(string title, string body) : this()
    {
        Text = title;
        SetReport(body);
    }

    /// <summary>
    /// Fills the report the way the Log tab fills its view: append the whole body with an explicit
    /// font and colour, instead of assigning <see cref="TextBox.Text"/>. Assigning Text replaces the
    /// RichEdit content in one shot and can leave its scroll range out of step with the themed
    /// scrollbars (the visible glitch while scrolling); appending lets each change resync them.
    /// </summary>
    private void SetReport(string body)
    {
        RichTextBox rtb = txtReport.InnerRichTextBox;
        rtb.Clear();
        rtb.SelectionStart = 0;
        rtb.SelectionLength = 0;
        rtb.SelectionColor = Color.Gainsboro;
        rtb.SelectionFont = new Font("Consolas", 9F);
        rtb.AppendText(body);
        rtb.SelectionStart = 0;
        rtb.SelectionLength = 0;
    }
}
