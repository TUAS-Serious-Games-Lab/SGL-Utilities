namespace SGL.Utilities.WinForms.Controls.LogGui {
	partial class LogMessageList {
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			lstMessages = new ListBox();
			SuspendLayout();
			// 
			// lstMessages
			// 
			lstMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lstMessages.DrawMode = DrawMode.OwnerDrawVariable;
			lstMessages.FormattingEnabled = true;
			lstMessages.HorizontalScrollbar = true;
			lstMessages.IntegralHeight = false;
			lstMessages.ItemHeight = 18;
			lstMessages.Location = new Point(1, 1);
			lstMessages.Margin = new Padding(0);
			lstMessages.Name = "lstMessages";
			lstMessages.Size = new Size(600, 498);
			lstMessages.TabIndex = 0;
			lstMessages.DrawItem += lstMessages_DrawItem;
			lstMessages.MeasureItem += lstMessages_MeasureItem;
			lstMessages.DoubleClick += lstMessages_DoubleClick;
			// 
			// LogMessageList
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add(lstMessages);
			Margin = new Padding(0);
			Name = "LogMessageList";
			Size = new Size(602, 501);
			Load += LogMessageList_Load;
			ResumeLayout(false);
		}

		#endregion

		private ListBox lstMessages;
	}
}
