using Microsoft.Extensions.Logging;
using SGL.Utilities.WinForms.Controls.LogGui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SGL.Utilities.WinForms.Controls.LogGui {
	[DefaultEvent(nameof(ItemDoubleClicked))]
	public partial class LogMessageList : UserControl {
		private Channel<LogMessage> pendingMessagesChannel = Channel.CreateUnbounded<LogMessage>(
			new UnboundedChannelOptions {
				AllowSynchronousContinuations = false,
				SingleReader = true,
				SingleWriter = false
			});
		private EventHandler<ItemDoubleClickedEventArgs>? itemDoubleClicked;
		private Color traceItemForeground = Color.Gray;
		private Color traceItemBackground = Color.White;
		private Color debugItemForeground = Color.Black;
		private Color debugItemBackground = Color.White;
		private Color infoItemForeground = Color.Black;
		private Color infoItemBackground = Color.LightGreen;
		private Color warningItemForeground = Color.Black;
		private Color warningItemBackground = Color.Yellow;
		private Color errorItemForeground = Color.Black;
		private Color errorItemBackground = Color.OrangeRed;
		private Color criticalItemForeground = Color.White;
		private Color criticalItemBackground = Color.DarkRed;
		private Color selectedItemForeground = SystemColors.HighlightText;
		private Color selectedItemBackground = SystemColors.Highlight;
		private SolidBrush traceItemForegroundBrush;
		private SolidBrush traceItemBackgroundBrush;
		private SolidBrush debugItemForegroundBrush;
		private SolidBrush debugItemBackgroundBrush;
		private SolidBrush infoItemForegroundBrush;
		private SolidBrush infoItemBackgroundBrush;
		private SolidBrush warningItemForegroundBrush;
		private SolidBrush warningItemBackgroundBrush;
		private SolidBrush errorItemForegroundBrush;
		private SolidBrush errorItemBackgroundBrush;
		private SolidBrush criticalItemForegroundBrush;
		private SolidBrush criticalItemBackgroundBrush;
		private SolidBrush selectedItemForegroundBrush;
		private SolidBrush selectedItemBackgroundBrush;
		private Func<LogMessage, string> formatItem = getMessageString;

		public LogMessageList() {
			traceItemForegroundBrush = new SolidBrush(traceItemForeground);
			traceItemBackgroundBrush = new SolidBrush(traceItemBackground);
			debugItemForegroundBrush = new SolidBrush(debugItemForeground);
			debugItemBackgroundBrush = new SolidBrush(debugItemBackground);
			infoItemForegroundBrush = new SolidBrush(infoItemForeground);
			infoItemBackgroundBrush = new SolidBrush(infoItemBackground);
			warningItemForegroundBrush = new SolidBrush(warningItemForeground);
			warningItemBackgroundBrush = new SolidBrush(warningItemBackground);
			errorItemForegroundBrush = new SolidBrush(errorItemForeground);
			errorItemBackgroundBrush = new SolidBrush(errorItemBackground);
			criticalItemForegroundBrush = new SolidBrush(criticalItemForeground);
			criticalItemBackgroundBrush = new SolidBrush(criticalItemBackground);
			selectedItemForegroundBrush = new SolidBrush(selectedItemForeground);
			selectedItemBackgroundBrush = new SolidBrush(selectedItemBackground);
			InitializeComponent();
		}


		[Browsable(true)]
		public event EventHandler<ItemDoubleClickedEventArgs>? ItemDoubleClicked {
			add => itemDoubleClicked += value;
			remove => itemDoubleClicked -= value;
		}

		public class ItemDoubleClickedEventArgs {
			public LogMessage Item { get; }

			public ItemDoubleClickedEventArgs(LogMessage item) {
				Item = item;
			}
		}

		private void lstMessages_DoubleClick(object sender, EventArgs e) {
			var msg = lstMessages.SelectedItem as LogMessage;
			if (msg != null) {
				itemDoubleClicked?.Invoke(this, new ItemDoubleClickedEventArgs(msg));
			}
		}

		public void ClearItems() {
			lstMessages.Items.Clear();
		}

		[Browsable(true)]
		[Category("Item Colors")]
		public Color TraceItemForeground {
			get => traceItemForeground; set {
				traceItemForeground = value;
				traceItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color TraceItemBackground {
			get => traceItemBackground; set {
				traceItemBackground = value;
				traceItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color DebugItemForeground {
			get => debugItemForeground; set {
				debugItemForeground = value;
				debugItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color DebugItemBackground {
			get => debugItemBackground; set {
				debugItemBackground = value;
				debugItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color InfoItemForeground {
			get => infoItemForeground; set {
				infoItemForeground = value;
				infoItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color InfoItemBackground {
			get => infoItemBackground; set {
				infoItemBackground = value;
				infoItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color WarningItemForeground {
			get => warningItemForeground; set {
				warningItemForeground = value;
				warningItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color WarningItemBackground {
			get => warningItemBackground; set {
				warningItemBackground = value;
				warningItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color ErrorItemForeground {
			get => errorItemForeground; set {
				errorItemForeground = value;
				errorItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color ErrorItemBackground {
			get => errorItemBackground; set {
				errorItemBackground = value;
				errorItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color CriticalItemForeground {
			get => criticalItemForeground; set {
				criticalItemForeground = value;
				criticalItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color CriticalItemBackground {
			get => criticalItemBackground; set {
				criticalItemBackground = value;
				criticalItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color SelectedItemForeground {
			get => selectedItemForeground; set {
				selectedItemForeground = value;
				selectedItemForegroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}
		[Browsable(true)]
		[Category("Item Colors")]
		public Color SelectedItemBackground {
			get => selectedItemBackground; set {
				selectedItemBackground = value;
				selectedItemBackgroundBrush = new SolidBrush(value);
				lstMessages.Invalidate();
			}
		}

		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Func<LogMessage, string> FormatItem {
			get => formatItem; set {
				formatItem = value;
				lstMessages.Invalidate();
			}
		}

		private void lstMessages_DrawItem(object sender, DrawItemEventArgs e) {
			if (e.Index < 0) {
				e.DrawBackground();
				e.DrawFocusRectangle();
				return;
			}
			Graphics g = e.Graphics;
			ListBox lb = (ListBox)sender;
			var item = lb.Items[e.Index];
			var msg = item as LogMessage;
			if (msg != null) {
				var foregroundBrush = msg.LogLevel switch {
					LogLevel.Trace => traceItemForegroundBrush,
					LogLevel.Debug => debugItemForegroundBrush,
					LogLevel.Information => infoItemForegroundBrush,
					LogLevel.Warning => warningItemForegroundBrush,
					LogLevel.Error => errorItemForegroundBrush,
					LogLevel.Critical => criticalItemForegroundBrush,
					_ => new SolidBrush(lstMessages.ForeColor)
				};
				var backgroundBrush = msg.LogLevel switch {
					LogLevel.Trace => traceItemBackgroundBrush,
					LogLevel.Debug => debugItemBackgroundBrush,
					LogLevel.Information => infoItemBackgroundBrush,
					LogLevel.Warning => warningItemBackgroundBrush,
					LogLevel.Error => errorItemBackgroundBrush,
					LogLevel.Critical => criticalItemBackgroundBrush,
					_ => new SolidBrush(lstMessages.BackColor)
				};
				if (e.State.HasFlag(DrawItemState.Selected)) {
					foregroundBrush = selectedItemForegroundBrush;
					backgroundBrush = selectedItemBackgroundBrush;
				}
				g.FillRectangle(backgroundBrush, e.Bounds);
				g.DrawString(FormatItem(msg), e.Font ?? lstMessages.Font, foregroundBrush, new PointF(e.Bounds.X, e.Bounds.Y));
			}
			else {
				g.DrawString(item.ToString(), e.Font ?? lstMessages.Font, new SolidBrush(e.ForeColor), new PointF(e.Bounds.X, e.Bounds.Y));
			}
			e.DrawFocusRectangle();
		}

		private static string getMessageString(LogMessage msg) {
			return $"[{msg.CategoryName}] {msg.FormattedMessage}";
		}

		internal async void AddItem(LogMessage msg) {
			if (!pendingMessagesChannel.Writer.TryWrite(msg)) {
				await pendingMessagesChannel.Writer.WriteAsync(msg);
			}
			if (lstMessages.Created) {
				lstMessages.Invoke(() => addPendingMessages());
			}
		}

		private void LogMessageList_Load(object sender, EventArgs e) {
			addPendingMessages();
		}

		private void addPendingMessages() {
			bool hadMessages = false;
			while (pendingMessagesChannel.Reader.TryRead(out var msg)) {
				lstMessages.Items.Add(msg);
				hadMessages = true;
			}
			if (hadMessages) {
				lstMessages.TopIndex = lstMessages.Items.Count - 1;
				int maxWidth = lstMessages.Items.OfType<LogMessage>().Max(
					msg => TextRenderer.MeasureText(FormatItem(msg), lstMessages.Font).Width);
				lstMessages.HorizontalExtent = maxWidth;
			}
		}

		private void lstMessages_MeasureItem(object sender, MeasureItemEventArgs e) {
			if (e.Index < 0) {
				return;
			}
			Graphics g = e.Graphics;
			ListBox lb = (ListBox)sender;
			var item = lb.Items[e.Index];
			var msg = item as LogMessage;
			if (msg != null) {
				var size = g.MeasureString(FormatItem(msg), lb.Font);
				e.ItemWidth = (int)MathF.Ceiling(size.Width);
				e.ItemHeight = (int)MathF.Ceiling(size.Height);
			}
		}
	}
}
