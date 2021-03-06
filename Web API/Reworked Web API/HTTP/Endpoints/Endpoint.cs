﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace API.HTTP.Endpoints
{
	/// <summary>
	/// An abstract class representing a webserver endpoint that can be invoked.
	/// </summary>
	public abstract class Endpoint
	{
		/// <summary>
		/// Gets the request object passed to this <see cref="Endpoint"/>.
		/// </summary>
		protected HttpListenerRequest Request { get; private set; }
		/// <summary>
		/// Gets the response object passed to this <see cref="Endpoint"/>.
		/// </summary>
		protected HttpListenerResponse Response { get; private set; }
		/// <summary>
		/// Gets the <see cref="HTTP.Server"/> instance that created this <see cref="Endpoint"/>.
		/// </summary>
		protected Server Server { get; private set; }

		/// <summary>
		/// Invokes the main function of this <see cref="Endpoint"/>.
		/// </summary>
		/// <param name="request">The request instance to pass to this <see cref="Endpoint"/>.</param>
		/// <param name="response">The response instance to pass to this <see cref="Endpoint"/>.</param>
		/// <param name="server">The <see cref="HTTP.Server"/> instance that created this <see cref="Endpoint"/>.</param>
		public void Invoke(HttpListenerRequest request, HttpListenerResponse response, Server server)
		{
			Request = request;
			Response = response;
			Server = server;
			Main();
		}

		/// <summary>
		/// Invokes the main function of this <see cref="Endpoint"/>.
		/// </summary>
		protected abstract void Main();

		/// <summary>
		/// Returns the type of an <see cref="Endpoint"/> subclass whose <see cref="EndpointUrl"/> attribute
		/// matches the specified <paramref name="url"/>, or null if none were found.
		/// </summary>
		/// <param name="url">The url of the endpoint.</param>
		/// <param name="asRegex">If true, interprets <paramref name="url"/> as a regular expression.</param>
		/// <exception cref="ArgumentNullException"/>
		public static Type GetEndpoint(string url, bool asRegex = false) => GetEndpoint<Endpoint>(url, asRegex);
		/// <summary>
		/// Returns the type of an <see cref="Endpoint"/> subclass whose <see cref="EndpointUrl"/> attribute
		/// matches the specified <paramref name="url"/>, or null if none were found.
		/// </summary>
		/// <typeparam name="T">The type that the returned endpoint must extend.</typeparam>
		/// <param name="url">The url of the endpoint.</param>
		/// <param name="asRegex">If true, interprets <paramref name="url"/> as a regular expression.</param>
		/// <exception cref="ArgumentNullException"/>
		public static Type GetEndpoint<T>(string url, bool asRegex = false) where T : Endpoint
		{
			// Regex url cannot be null
			if (asRegex && url == null) throw new ArgumentNullException("url", "Url used as RegEx cannot be null");
			// Get the assembly of type T and iterate through it's types
			Assembly asm = typeof(T).Assembly;
			foreach (var type in asm.GetTypes())
			{
				// Every non abstract class is tested
				if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(T)))
				{
					// Get all EndpointUrl attributes and see if they match the given url
					var attributes = type.GetCustomAttributes(typeof(EndpointUrl)).Cast<EndpointUrl>();
					if (!asRegex && attributes.Any(x => x.Url == url.ToLower())) return type;
					else if (asRegex && attributes.Any(x => Regex.IsMatch(x.Url, url))) return type;
				}
			}
			return null;
		}

		/// <summary>
		/// Returns the url of the specified endpoint class.
		/// </summary>
		/// <typeparam name="T">A subclass of <see cref="Endpoint"/>.</typeparam>
		public static string GetUrl<T>() where T : Endpoint
			=> typeof(T).GetCustomAttribute<EndpointUrl>()?.Url;

		/// <summary>
		/// Directs the request to <paramref name="src"/> to a new instance of the specified type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">An <see cref="Endpoint"/> subclass to direct the request to.</typeparam>
		/// <param name="src">The <see cref="Endpoint"/> whose request to redirect to another endpoint.</param>
		public static void InternalRedirect<T>(Endpoint src) where T : Endpoint
			=> (Activator.CreateInstance(typeof(T)) as Endpoint).Invoke(src.Request, src.Response, src.Server);

		/// <summary>
		/// Splits a url query into a dictionary.
		/// </summary>
		protected static Dictionary<string, string> SplitQuery(HttpListenerRequest request)
		{
			string query;
			// Post requests keep their query string inside their payload
			if (request.ContentType == "application/x-www-form-urlencoded")
			{
				using var reader = new StreamReader(request.InputStream);
				query = reader.ReadToEnd();
			}
			else
			{
				query = request.Url.Query;
				// Strip the '?' from the query if it is specified
				if (query.Length > 1) query = query[1..];
			}
			// Return empty dict if there is no query string
			if (query.Length == 0) return new Dictionary<string, string>();

			// Split the query string into key-value pairs
			var items = query.Split('&');
			var outDict = new Dictionary<string, string>();
			foreach (var item in items)
			{
				// Try to parse every key-value pair
				var keyValuePair = item.Replace("+", " ").Split('=', 2);
				outDict[Uri.UnescapeDataString(keyValuePair[0])] = Uri.UnescapeDataString(string.Concat(keyValuePair[1..]));
			}
			return outDict;
		}
	}
}