﻿using API.Attributes;
using API.Database;
using API.HTTP.Endpoints;
using API.HTTP.Filters;
using MimeKit;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace API.HTTP
{
	/// <summary>
	/// A custom <see cref="Server"/> class that exclusively serves pages (most commonly html pages) to clients like browsers.
	/// </summary>
	public sealed class HTMLServer : Server
	{
		/// <summary>
		/// Gets the <see cref="User"/> instance associated with the session that is issuing the request.
		/// </summary>
		private User CurrentUser
		{
			get
			{
				// Skip if the session is null or does not have a userid
				if (CurrentSession == null || !CurrentSession.User.HasValue) return null;
				// Set the cache with a user from the database if it isn't already set
				if (_CurrentUser == null) _CurrentUser = Database.Select<User>($"`id` = {CurrentSession.User}").FirstOrDefault();
				// Return the cache
				return _CurrentUser;
			}
		}
		private User _CurrentUser;
		/// <summary>
		/// Gets the <see cref="Session"/> instance that is issuing the request.
		/// </summary>
		private Session CurrentSession { get; set; }
		/// <summary>
		/// Gets the <see cref="AppDatabase"/> of the current thread.
		/// </summary>
		private AppDatabase Database => Utils.GetDatabase();

		/// <summary>
		/// Diagnostics timer for detailed log messages.
		/// </summary>
		private Stopwatch Timer { get; } = new Stopwatch();

		/// <summary>
		/// Creates a new instance of <see cref="HTMLServer"/>.
		/// </summary>
		/// <param name="queue">The source of requests for this <see cref="HTMLServer"/>.</param>
		public HTMLServer(BlockingCollection<HttpListenerContext> queue) : base(queue) { }

		protected override void Main()
		{
            // Reset cached user
            _CurrentUser = null;

			// Print log and start diagnostics timer
			Program.Log.Fine($"Processing {Request.HttpMethod} request for '{Request.Url.AbsolutePath}'...");
			Timer.Restart();

			var url = Request.Url.AbsolutePath.ToLower();

			// Get the session from the cookies (if it exists)
			var sessionId = Request.Cookies["session"]?.Value;
			CurrentSession = sessionId == null ? null : Utils.GetSession(sessionId);

			// Apply redirects
			var redirect = Utils.Redirects.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.HTML) != 0 && x.Target == url);
			if (redirect != null)
			{
				// Send a 308 Permanent Redirect
				Response.Redirect(redirect.Redirect);
				SendError(HttpStatusCode.PermanentRedirect);
				return;
			}

			// Apply aliases
			var alias = Utils.Aliases.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.HTML) != 0 && (x.Target == url || x.Alias == url));
			if (alias != null)
			{
				if (alias.HideTarget && url == alias.Target)
				{
					// Send 404 Not Found if the target was requested but should be hidden
					SendError(HttpStatusCode.NotFound);
					return;
				}
				// Replace the requested url with the actual target url
				url = alias.Target;
			}
			Main(url);
		}
		/// <summary>
		/// Seconday main function used request reinterpretation.
		/// </summary>
		/// <param name="url">The url to parse.</param>
		/// <remarks>
		/// This function was nescessary to implement the ErrorPageAttribute in the most abstracted way possible.
		/// </remarks>
		private void Main(string url)
		{
			Response.ContentType = "text/html";

			// Check for login requirement (used later for every case where an endpoint is found)
			var requiresLogin = Utils.LoginRequirements.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.HTML) != 0 && x.Target == url);
	
			// Find and invoke all url filters
			foreach (var filterType in Filter.GetFilters(url))
			{
				var filter = Activator.CreateInstance(filterType) as Filter;
				// If invoke returned false, then further url parsing should be interrupted.
				if (!filter.Invoke(Request, Response, this)) return;
			}

			// Find an endpoint
			var endpoint = Endpoint.GetEndpoint<HTMLEndpoint>(url);
			if (endpoint != null)
			{
				// Show 401 if login is required
				if (requiresLogin != null)
				{
					SendError(HttpStatusCode.Unauthorized);
					return;
				}

				// Create an instance of the endpoint
				var endpointInstance = (Activator.CreateInstance(endpoint) as HTMLEndpoint);
				endpointInstance.CurrentSession = CurrentSession;
				endpointInstance.CurrentUser = CurrentUser;
				endpointInstance.Invoke(Request, Response, this);
				return;
			}

			// Replace blank url with index.html
			if (url == "/") url = "/index.html";

			// Try to find a file endpoint
			string file = Program.Config.HTMLSourceDir + Uri.UnescapeDataString(url);
			if (File.Exists(file))
			{
				// Show 401 if login is required
				if (requiresLogin != null)
				{
					SendError(HttpStatusCode.Unauthorized);
					return;
				}

				Response.AddHeader("Date", Utils.FormatTimeStamp(File.GetLastWriteTimeUtc(file)));
				SendFile(file);
				return;
			}

			// Try to serve a custom page if an image was requested
			file = Program.Config.ResourceDir + Uri.UnescapeDataString(url);
			if (File.Exists(file) && Request.AcceptTypes.Any(x => x.Contains("image/")))
			{
				// Show 401 if login is required
				if (requiresLogin != null)
				{
					SendError(HttpStatusCode.Unauthorized);
					return;
				}

				Response.AddHeader("Date", Utils.FormatTimeStamp(File.GetLastWriteTimeUtc(file)));
				ServeImage(Response, url);
				return;
			}

			// Send 404 if no endpoint is found
			SendError(HttpStatusCode.NotFound);
		}

		/// <summary>
		/// Custom function that sends a custom page for image requests.
		/// </summary>
		/// <param name="response"></param>
		private void ServeImage(HttpListenerResponse response, string file)
		{
			if (new string[] { ".webm", ".mp4", ".ogg" }.Contains(Path.GetExtension(file)))
			{
				response.AddHeader("Accept-Ranges", "bytes");
				// TODO implement these shenanigans in a neat (hopefully .cshtml) template
				SendText("<html style=\"text-align: center\">" +
					"<body style=\"background-color: black; margin: 0; padding: 0;\">" +
					"<video controls style=\"width: 100%; max-height: 100vh;\">" +
					$"<source src=\"{file}\" type=\"{MimeTypes.GetMimeType(Path.GetExtension(file))}\">" +
					"</video>" +
					"</body>" +
					"</html>");
				return;
			}
			SendFile(Program.Config.ResourceDir + Uri.UnescapeDataString(file));
		}

		#region Overrides
		/// <summary>
		/// Writes a byte array to the specified <see cref="HttpListenerResponse"/>.
		/// </summary>
		/// <param name="data">The array of bytes to send.</param>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to send to the client.</param>
		public override void Send(byte[] data, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			base.Send(data, StatusOverride ?? statusCode);
			// Write detailed log about response
			var logMessage = $"Processed  {Request.HttpMethod} request for '{Request.Url.AbsolutePath}' with status code {(int)statusCode} " +
					$"in {Utils.FormatTimer(Timer)}{(data == null ? "" : $" and sent {Utils.FormatDataLength(data.Length)}")}.";
			// Success status codes are seen as less important, thus are trace messages
			if (((int)statusCode).ToString().StartsWith("2")) Program.Log.Trace(logMessage);
			else Program.Log.Info(logMessage);
		}
		/// <summary>
		/// Writes plain text to the specified <see cref="HttpListenerResponse"/>.
		/// </summary>
		/// <param name="text">The string of text to send.</param>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to send to the client.</param>
		/// <param name="encoding">The encoding of the text. <see cref="Encoding.UTF8"/> by default.</param>
		public override void SendText(string text, HttpStatusCode statusCode = HttpStatusCode.OK, Encoding encoding = null)
			=> base.SendText(text, StatusOverride ?? statusCode, encoding);
		/// <summary>
		/// Writes a <see cref="JObject"/> to the specified <see cref="HttpListenerResponse"/>.
		/// </summary>
		/// <param name="json">The <see cref="JObject"/> to send to the client.</param>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to send to the client.</param>
		public override void SendJSON(JObject json, HttpStatusCode statusCode = HttpStatusCode.OK)
			=> base.SendJSON(json, StatusOverride ?? statusCode);
		/// <summary>
		/// Sends all the data of the specified file and automatically provides the correct MIME type to the client.
		/// </summary>
		/// <param name="path">The path to the file to send.</param>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to send to the client.</param>
		public override void SendFile(string path, HttpStatusCode statusCode = HttpStatusCode.OK)
			=> base.SendFile(path, StatusOverride ?? statusCode);

		/// <summary>
		/// Used for custom error pages.
		/// </summary>
		private HttpStatusCode? StatusOverride;
		/// <summary>
		/// Used for detecting infinite loops in cursom error pages.
		/// </summary>
		private HttpStatusCode? PreviousCode;

		/// <summary>
		/// Sends just an <see cref="HttpStatusCode"/> to the client.
		/// </summary>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to specify.</param>
		public override void SendError(HttpStatusCode statusCode)
		{
			// Try to find an errorpage attribute
			var errorPage = Utils.ErrorPages.FirstOrDefault(x => x.StatusCode == statusCode);
			if (errorPage != null)
			{
				// Infinite loop detection
				if (errorPage.Url == Request.Url.AbsoluteUri.ToLower() || statusCode == PreviousCode)
				{
					Program.Log.Trace($"Caught infinite loop for error page '{errorPage.Url}' for status code {(int)statusCode}.");
					// Determine possible cause
					string cause = statusCode switch
					{
						HttpStatusCode.NotFound => "The error page may be missing.",
						HttpStatusCode.InternalServerError => "The error page itself may be throwing an unhandled exception.",
						HttpStatusCode.Forbidden => "The error page may be inaccessible.",
						HttpStatusCode.Unauthorized => "The error page may require the user to be logged in.",
						HttpStatusCode.NotImplemented => "The error page may be missing the requested HTTP method.",
						_ => null
					};
					if (cause != null) Program.Log.Trace(cause);
					Program.Log.Trace("Please check Properties.cs");
					// Reset and fall back to default implementation
					PreviousCode = null;
					base.SendError(statusCode);
					return;
				}

				// If it is a redirect, simply send a 302 Redirect status code
				if (errorPage.IsRedirect)
				{
					Response.Redirect(errorPage.Url);
					SendError(HttpStatusCode.PermanentRedirect);
					return;
				}

				// Cache the status code for infinite loop detection
				PreviousCode = statusCode;
				// If specified, overrride the next status code and parse the error page url instead and send that endpoint
				if (errorPage.KeepStatusCode) StatusOverride = statusCode;
				try
				{
					Main(errorPage.Url);
					return;
				}
				finally
				{
					// Reset control values
					StatusOverride = null;
					PreviousCode = null;
				}
			}
			base.SendError(statusCode);
		}
		#endregion
	}
}
