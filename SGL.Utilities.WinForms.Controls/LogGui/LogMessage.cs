using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.WinForms.Controls.LogGui {
	public class LogMessage {
		public LogMessage(string categoryName, List<string> formattedSopes, LogLevel logLevel, EventId eventId, DateTime time, string formattedMessage, Exception? exception) {
			CategoryName = categoryName;
			FormattedSopes = formattedSopes;
			LogLevel = logLevel;
			EventId = eventId;
			Time = time;
			FormattedMessage = formattedMessage;
			Exception = exception;
		}

		public Exception? Exception { get; }
		public string CategoryName { get; }
		public List<string> FormattedSopes { get; }
		public LogLevel LogLevel { get; }
		public EventId EventId { get; }
		public DateTime Time { get; }
		public string FormattedMessage { get; }
	}
}
