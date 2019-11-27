﻿using System;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Reflection;

namespace API.HTTP.Endpoints
{
	/// <summary>
	/// An abstract subclass of <see cref="Endpoint"/>. This contains functions specifically made to handle a JSON type request.
	/// </summary>
	/// <remarks>
	/// To make actual JSON endpoints you must extend this class and implement the abstract classes. At least one of these functions
	/// must write data to the response object. If nothing is sent, a 501 will be sent instead.
	/// Attributes must be used to specify the target url.
	/// Because reflection is used to invoke a particular HTTP method, additional HTTP method support can be implemented by simply
	/// making a new public function, whose name is the HTTP method it represents in all upper case. Note that they must take the
	/// same parameters as the other functions.
	/// </remarks>
	public abstract class JsonEndpoint : Endpoint
	{
		/// <summary>
		/// Extracts the parameters and JSON from the request object and calls the specified HTTP method function.
		/// </summary>
		protected override void Main()
		{
			// Read the inputstream of the request and try to convert it to a JObject
			JObject content;
			try
			{
				// If content length is 0 (no content) then use blank JObject
				if (Request.ContentLength64 == 0) content = new JObject();
				else
				{
					string body;
					// If the data isn't a json, expect encoded data
					if (Request.ContentType != "application/json")
					{
						// Send error if the request is missing encryption data
						if (!Utils.IsRequestEncrypted(Request))
						{
							Server.SendError(HttpStatusCode.BadRequest);
							return;
						}

						// Get iv and session from request headers and cookies
						var iv = Convert.FromBase64String(Request.Headers.Get("Content-IV"));
						var session = Request.Cookies["session"].Value;
						
						// Get all bytes from the request body
						using var mem = new MemoryStream();
						Request.InputStream.CopyTo(mem);
						mem.Close();

						// Decrypt the body
						body = Encoding.UTF8.GetString(Utils.AESDecrypt(session, mem.ToArray(), iv));
					}
					else
					{
						// Read all text from inputstream
						using var reader = new StreamReader(Request.InputStream, Request.ContentEncoding);
						body = reader.ReadToEnd();
					}
					content = JObject.Parse(body);
				}
			}
			catch (JsonReaderException)
			{
				// Send BadRequest if it doesn't contain a readable JSON
				Server.SendError(HttpStatusCode.BadRequest);
				return;
			}
			var parameters = SplitQuery(Request);

			// Invoke the right http method function
			var method = GetType().GetMethod(Request.HttpMethod.ToUpper());

			// If the method requires encrypted data but the request is unencrypted, send a 400 Bad Request.
			if (method.GetCustomAttribute<RequiresEncryptionAttribute>() != null && !Utils.IsRequestEncrypted(Request))
			{
				Server.SendError(HttpStatusCode.BadRequest);
				return;
			}

			// Invoke the method, or send a 501 not implemented
			if (method == null) Server.SendError(HttpStatusCode.NotImplemented);
			else method.Invoke(this, new object[] { content, parameters });
		}

		/// <summary>
		/// Endpoint for the http GET method. This must be implemented.
		/// </summary>
		/// <param name="json">The json sent by the client.</param>
		/// <param name="parameters">A dictionary containing all url parameters.</param>
		public abstract void GET(JObject json, Dictionary<string, string> parameters);
		/// <summary>
		/// Endpoint for the http POST method.
		/// </summary>
		/// <param name="json">The json sent by the client.</param>
		/// <param name="parameters">A dictionary containing all url parameters.</param>
		public virtual void POST(JObject json, Dictionary<string, string> parameters) => Server.SendError(HttpStatusCode.NotImplemented);
		/// <summary>
		/// Endpoint for the http DELETE method.
		/// </summary>
		/// <param name="json">The json sent by the client.</param>
		/// <param name="parameters">A dictionary containing all url parameters.</param>
		public virtual void DELETE(JObject json, Dictionary<string, string> parameters) => Server.SendError(HttpStatusCode.NotImplemented);
		/// <summary>
		/// Endpoint for the http PATCH method.
		/// </summary>
		/// <param name="json">The json sent by the client.</param>
		/// <param name="parameters">A dictionary containing all url parameters.</param>
		public virtual void PATCH(JObject json, Dictionary<string, string> parameters) => Server.SendError(HttpStatusCode.NotImplemented);

		/// <summary>
		/// Validates an array of parameters by checking if they are present and match the predicate. A response is
		/// immediately sent to the client if this function returns false.
		/// </summary>
		/// <param name="json">The json from which to get the values.</param>
		/// <param name="predicates">An array of tuples containing the parameter name and a predicate.</param>
		/// <returns>True if the validation succeeded. False otherwise.</returns>
		protected bool ValidateParams(JObject json, params (string, Func<JToken, bool>)[] predicates)
			=> ValidateParams(json, ValidationMode.Required, predicates);
		/// <summary>
		/// Validates an array of parameters by checking if they are present and match the predicate. A response is
		/// immediately sent to the client if this function returns false.
		/// </summary>
		/// <param name="json">The json from which to get the values.</param>
		/// <param name="mode">The type of validation checking to use.</param>
		/// <param name="predicates">An array of tuples containing the parameter name and a predicate.</param>
		/// <returns>True if the validation succeeded. False otherwise.</returns>
		protected bool ValidateParams(JObject json, ValidationMode mode, params (string, Func<JToken, bool>)[] predicates)
		{
			// Get all missing parameters
			var missing = new List<string>();
			foreach (var (name, predicate) in predicates)
				if (!json.ContainsKey(name))
					missing.Add(name);

			// Check predicates for every value that isn't missing
			var invalid = new List<string>();
			foreach (var (name, predicate) in predicates.Where(x => !missing.Contains(x.Item1)))
				if (!predicate(json[name]))
					invalid.Add(name);

			var outJson = new JObject();
			if (mode == ValidationMode.Required && missing.Any()) outJson.Add("missing", new JArray(missing));
			if (mode == ValidationMode.Options && missing.Count == predicates.Count())
				outJson.Add("missing", new JObject() { "options", new JArray(missing) });

			// Get every param where their predicate returns false
			var failed = invalid.Where(x => predicates.First(y => x == y.Item1).Item2(json.GetValue(x)));
			if (failed.Any()) outJson.Add("invalid", new JArray(failed));

			// Return true if the validation didnt encounter errors
			if (outJson.Count == 0) return true;
			// Send the json if there were errors
			Server.SendJSON(outJson, HttpStatusCode.UnprocessableEntity);
			return false;
		}

		/// <summary>
		/// Specifies how the parameter validation will behave.
		/// </summary>
		protected enum ValidationMode
		{
			/// <summary>
			/// All specified parameters are required and must pass validation.
			/// </summary>
			Required,
			/// <summary>
			/// At least one of the specified parameters are required and all given parameters must pass validation.
			/// </summary>
			Options,
			/// <summary>
			/// None of the parameters are required, but all given parameters must pass validation.
			/// </summary>
			Optional
		}
	}
}
