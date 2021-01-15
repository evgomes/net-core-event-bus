using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetCoreEventBus.Web.Extensions
{
	public static class ModelStateExtensions
	{
		public static string GetErrorMessage(this ModelStateDictionary dictionary)
		{
			var formattedError = dictionary
				.SelectMany(m => m.Value.Errors)
				.Select(m => m.ErrorMessage)
				.Aggregate((a, b) => string.Concat(a, " - ", b));

			return formattedError;
		}
	}
}
