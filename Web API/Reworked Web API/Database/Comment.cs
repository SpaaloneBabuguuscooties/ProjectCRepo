﻿using MySQL.Modeling;
using System;

namespace API.Database
{
	[Table("comments")]
	public sealed class Comment : ItemAdapter
	{
		[AutoIncrement]
		public int? Id { get; set; }
		public int Task { get; set; }
		public int? Creator { get; set; }
		public string Message { get; set; }
		public long Created { get; set; } = DateTimeOffset.Now.ToUnixTimeSeconds();
		public long? Edited { get; set; }
	}
}
