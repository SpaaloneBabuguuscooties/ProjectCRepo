﻿using MySql.Data.MySqlClient;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace MySQL.Modeling
{
	/// <summary>
	/// Abstract class for modeling a MySQL table item into an object.
	/// </summary>
	/// <remarks>
	/// Subclasses of <see cref="ItemAdapter"/> should specify a name with the <see cref="TableAttribute"/> attribute.
	/// Otherwise, the class name will be used.
	/// </remarks>
	public abstract class ItemAdapter : IComparable<ItemAdapter>, ICloneable
	{
		/// <summary>
		/// Returns a string representation of this <see cref="ItemAdapter"/>.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			string selector(PropertyInfo x)
			{
				var data = Utils.GetColumnData(x);
				var txt = x.GetValue(this)?.ToString();
				if (txt == null) txt = "NULL";
				else if (x.PropertyType == typeof(string)) txt = '"' + txt + '"';
				return $"{data.Name}: {txt}";
			};
			return GetType().Name + "(" + string.Join(", ", Utils.GetAllColumns(GetType()).Select(selector)) + ")";
		}

		/// <summary>
		/// Determines whether the specified object is of the same type as this and contains the same elements.
		/// </summary>
		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (base.Equals(obj)) return true;
			if (obj.GetType() == GetType())
			{
				// Checks if a equals b, or if a and b are both null
				static bool isEqual(object a, object b) => a?.Equals(b) ?? a == b;
				// Check if the columns are equal for both objects
				return Utils.GetAllColumns(GetType()).All(x => isEqual(x.GetValue(this), x.GetValue(obj)));
			}
			return false;
		}

		public override int GetHashCode() => base.GetHashCode();

		/// <summary>
		/// Compares the elements of the other item if it's type is equal to this. Otherwise compares
		/// the other object based on it's type name.
		/// </summary>
		/// <param name="other">The other <see cref="ItemAdapter"/> to compare this to.</param>
		public int CompareTo([AllowNull] ItemAdapter other)
		{
			// Always return 1 if the other is null
			if (other == null) return 1;
			// If the types are equal, compare based on column values
			if (other.GetType() == GetType())
			{
				static int compare(object a, object b) => (a as IComparable)?.CompareTo(b) ?? 0;
				foreach (var x in Utils.GetAllColumns(GetType()))
				{
					// Compare every column and return the first non-zero compare result
					var compareVal = compare(x.GetValue(this), x.GetValue(other));
					if (compareVal != 0) return compareVal;
				}
				// Return 0 if all compares resulted in 0
				return 0;
			}
			// Compare name if the types aren't equal
			return GetType().Name.CompareTo(other.GetType().Name);
		}

		[IgnoreProperty]
		internal bool IsChanged => clone == null ? false : !Equals(clone);
		internal ItemAdapter clone = null;
		/// <summary>
		/// Stores a clone of this instance internally for use in the update methods of <see cref="DatabaseAdapter"/>.
		/// This will overwrite the current cache.
		/// </summary>
		public void Cache() => clone = Clone() as ItemAdapter;

		/// <summary>
		/// Returns a new <see cref="ItemAdapter"/> instance with column values equal to this.
		/// </summary>
		public object Clone()
		{
			var clone = Activator.CreateInstance(GetType()) ?? throw new InvalidOperationException("Cannot create new instance of " + GetType().Name);
			// Get all properties and set copy the values to the clone
			foreach (var column in Utils.GetAllColumns(GetType()))
				column.SetValue(clone, column.GetValue(this));
			// Return this instance since the clone is only used internally
			return clone;
		}

		/// <summary>
		/// Indicates if two items are of the same type and contain the same values.
		/// </summary>
		public static bool operator ==(ItemAdapter item1, ItemAdapter item2) => item1 as object == null || item1.Equals(item2);
		/// <summary>
		/// Indicates if two items aren't of the same type or don't contain the same values.
		/// </summary>
		public static bool operator !=(ItemAdapter item1, ItemAdapter item2) => !(item1 == item2);
	}
}
