﻿using API.Attributes;
using API.Database;
using API.HTTP.Endpoints;
using API.HTTP.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace API.HTTP
{
	/// <summary>
	/// A custom <see cref="Server"/> instance that only accepts JSON type requests.
	/// </summary>
	public sealed class JsonServer : Server
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
		/// Creates a new instance of <see cref="JsonServer"/>.
		/// </summary>
		/// <param name="queue">The source of requests for this <see cref="JsonServer"/>.</param>
		public JsonServer(BlockingCollection<HttpListenerContext> queue) : base(queue) { }

		protected override void Main()
		{
            // Reset cached user
            _CurrentUser = null;

            // Print log and start diagnostics timer
            Program.Log.Fine($"Processing {Request.HttpMethod} request for '{Request.Url.AbsolutePath}'...");
			Timer.Restart();

			Response.ContentType = "application/json";
			string url = Request.Url.AbsolutePath.ToLower();

			// Get the session from the cookies (if it exists)
			var sessionId = Request.Cookies["session"]?.Value;
			CurrentSession = sessionId == null ? null : Utils.GetSession(sessionId);

			// Check for login requirement (used later for every case where an endpoint is found)
			var requiresLogin = Utils.LoginRequirements.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.HTML) != 0 && x.Target == url);

			// Apply redirects
			var redirect = Utils.Redirects.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.JSON) != 0 && x.Target == url);
			if (redirect != null)
			{
				// Send a 301 Permanent Redirect
				Response.Redirect(redirect.Redirect);
				SendError(HttpStatusCode.PermanentRedirect);
				return;
			}

			// Apply aliases
			var alias = Utils.Aliases.FirstOrDefault(x => (x.ValidOn & ServerAttributeTargets.JSON) != 0 && (x.Target == url || x.Alias == url));
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

			// Find all url filters
			foreach (var filterType in Filter.GetFilters(url))
			{
				var filter = Activator.CreateInstance(filterType) as Filter;
				// If invoke returned false, then further url parsing should be interrupted.
				if (!filter.Invoke(Request, Response, this)) return;
			}

			// Find an endpoint
			var endpoint = Endpoint.GetEndpoint<JsonEndpoint>(url);
			if (endpoint != null)
			{
				// Show 401 if login is required
				if (requiresLogin != null)
				{
					SendError(HttpStatusCode.Unauthorized);
					return;
				}

				// Create an instance of the endpoint
				var endpointInstance = (Activator.CreateInstance(endpoint) as JsonEndpoint);
				endpointInstance.CurrentSession = CurrentSession;
				endpointInstance.CurrentUser = CurrentUser;
				endpointInstance.Invoke(Request, Response, this);

				// Close the response if the endpoint didn't close it
				try { Response.Close(); }
				catch (ObjectDisposedException) { }
				return;
			}

			// Send 404 if no endpoint is found
			SendError(HttpStatusCode.NotFound);
		}

		/// <summary>
		/// Writes a byte array to the specified <see cref="HttpListenerResponse"/>.
		/// </summary>
		/// <param name="data">The array of bytes to send.</param>
		/// <param name="statusCode">The <see cref="HttpStatusCode"/> to send to the client.</param>
		/// <remarks>
		/// This method also automatically encrypts the outgoing data if the request contained an IV and session id.
		/// </remarks>
		public override void Send(byte[] data, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			if (data != null && Utils.IsRequestEncrypted(Request))
			{
				Response.StatusCode = (int)statusCode;
				Response.ContentType = "application/octet-stream";

				// Encode data and add IV header
				var sessionId = Request.Cookies["session"].Value;
				data = Utils.AESEncrypt(sessionId, data, out byte[] iv);
				Response.AddHeader("Content-IV", Convert.ToBase64String(iv));
			}
			base.Send(data, statusCode);
			// Write detailed log about response
			var logMessage = $"Processed  {Request.HttpMethod} request for '{Request.Url.AbsolutePath}' with status code {(int)statusCode} " +
					$"in {Utils.FormatTimer(Timer)}{(data == null ? "" : $" and sent {Utils.FormatDataLength(data.Length)}")}.";
			// Success status codes are seen as less important, thus are trace messages
			if (((int)statusCode).ToString().StartsWith("2")) Program.Log.Trace(logMessage);
			else Program.Log.Info(logMessage);
		}
	}
}
