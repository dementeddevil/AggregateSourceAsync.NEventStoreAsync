using System;

namespace NUnit.Framework
{
	public static class AssertEx
	{
		public static TException ThrowsAsync<TException>(Action method)
			where TException : Exception
		{
			TException result = null;
			try
			{
				method();
			}
			catch(AggregateException exception)
			{
				Assert.That(typeof(TException).IsInstanceOfType(exception.InnerException));
				result = exception.InnerException as TException;
			}
			catch(Exception exception)
			{
				Assert.That(typeof(TException).IsInstanceOfType(exception));
				result = exception as TException;
			}
			return result;
		}
	}
}
