using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioSync
{
    public static class Extensions
    {
        /// <summary>
        /// Tell subscribers, if any, that this event has been raised.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The generic event handler</param>
        /// <param name="sender">this or null, usually</param>
        /// <param name="args">Whatever you want sent</param>
        public static void Raise<T>(this EventHandler<T> handler, object? sender, T args) where T : EventArgs
        {
            // Copy to temp var to be thread-safe (taken from C# 3.0 Cookbook - don't know if it's true)
            EventHandler<T> copy = handler;
            copy?.Invoke(sender, args);
        }

        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <param name="memberSelector">The member selector.</param>
        /// <returns></returns>
        public static string? GetMemberName(this LambdaExpression memberSelector)
        {
            static string? nameSelector(Expression e)
            {
                return e.NodeType switch
                {
                    ExpressionType.Parameter => ((ParameterExpression)e).Name,
                    ExpressionType.MemberAccess => ((MemberExpression)e).Member.Name,
                    ExpressionType.Call => ((MethodCallExpression)e).Method.Name,
                    ExpressionType.Convert or ExpressionType.ConvertChecked => nameSelector(((UnaryExpression)e).Operand),
                    ExpressionType.Invoke => nameSelector(((InvocationExpression)e).Expression),
                    ExpressionType.ArrayLength => "Length",
                    _ => throw new DataException("not a proper member selector"),
                };
            }

            return nameSelector(memberSelector.Body);
        }

        /// <summary>
        /// Truncates the specified maximum length.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="maxLength">The maximum length.</param>
        /// <returns></returns>
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public static bool IsValidFileName(this string fileName)
        {
            System.IO.FileInfo? fi = null;
            try
            {
                fi = new System.IO.FileInfo(fileName);
            }
            catch (ArgumentException) { }
            catch (System.IO.PathTooLongException) { }
            catch (NotSupportedException) { }
            if (fi is null) return false;
            if (!System.IO.Path.IsPathRooted(fileName)) return false;
            foreach (var component in fileName.Split('\\', '.'))
            {
                if (component.Length > 8) return false;
            }
            return true;
        }
    }
}
