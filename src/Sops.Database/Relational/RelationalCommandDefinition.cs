using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace Sops.Database.Relational
{
    /// <summary>
    /// Represents an immutable relational command definition containing SQL text, parameters, transaction context, and idempotency semantics.
    /// Ensures parameters are safely passed via parameterized <see cref="DbParameter"/> objects rather than string concatenation.
    /// </summary>
    public sealed class RelationalCommandDefinition
    {
        private static readonly IReadOnlyDictionary<string, object?> EmptyParameters =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the SQL query or command text.
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// Gets the command parameters by name.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        /// <summary>
        /// Gets the command type (Text, StoredProcedure, or TableDirect).
        /// </summary>
        public CommandType CommandType { get; }

        /// <summary>
        /// Gets the optional command execution timeout override in seconds.
        /// </summary>
        public int? TimeoutSeconds { get; }

        /// <summary>
        /// Gets the active transaction in which this command executes, if any.
        /// </summary>
        public DbTransaction? Transaction { get; }

        /// <summary>
        /// Gets a value indicating whether this command is idempotent (safe to retry on transient failure).
        /// Defaults to <c>false</c> to prevent unintended duplicate writes.
        /// </summary>
        public bool IsIdempotent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalCommandDefinition"/> class.
        /// </summary>
        /// <param name="commandText">The SQL command text. Must not be null or empty.</param>
        /// <param name="parameters">Optional dictionary of parameter names and values.</param>
        /// <param name="commandType">The command type. Defaults to <see cref="CommandType.Text"/>.</param>
        /// <param name="timeoutSeconds">Optional command timeout in seconds.</param>
        /// <param name="transaction">Optional active database transaction.</param>
        /// <param name="isIdempotent">Whether the command is idempotent. Defaults to false.</param>
        public RelationalCommandDefinition(
            string commandText,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CommandType commandType = CommandType.Text,
            int? timeoutSeconds = null,
            DbTransaction? transaction = null,
            bool isIdempotent = false)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                throw new ArgumentException("Command text cannot be null, empty, or whitespace.", nameof(commandText));
            }

            CommandText = commandText;
            Parameters = parameters ?? EmptyParameters;
            CommandType = commandType;
            TimeoutSeconds = timeoutSeconds;
            Transaction = transaction;
            IsIdempotent = isIdempotent;
        }

        /// <summary>
        /// Creates a <see cref="RelationalCommandDefinition"/> from command text and an optional anonymous object or dictionary of parameters.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">An anonymous object or dictionary representing parameters.</param>
        /// <param name="commandType">The command type.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <param name="isIdempotent">Whether the command is idempotent.</param>
        /// <returns>A new <see cref="RelationalCommandDefinition"/> instance.</returns>
        public static RelationalCommandDefinition Create(
            string commandText,
            object? parameters = null,
            CommandType commandType = CommandType.Text,
            int? timeoutSeconds = null,
            DbTransaction? transaction = null,
            bool isIdempotent = false)
        {
            IReadOnlyDictionary<string, object?> parameterMap = ExtractParameters(parameters);
            return new RelationalCommandDefinition(commandText, parameterMap, commandType, timeoutSeconds, transaction, isIdempotent);
        }

        /// <summary>
        /// Creates an idempotent read-only or idempotent write command definition.
        /// </summary>
        /// <param name="commandText">The SQL command text.</param>
        /// <param name="parameters">Optional parameters.</param>
        /// <param name="commandType">The command type.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds.</param>
        /// <param name="transaction">Optional transaction.</param>
        /// <returns>An idempotent <see cref="RelationalCommandDefinition"/> instance.</returns>
        public static RelationalCommandDefinition Idempotent(
            string commandText,
            object? parameters = null,
            CommandType commandType = CommandType.Text,
            int? timeoutSeconds = null,
            DbTransaction? transaction = null)
        {
            return Create(commandText, parameters, commandType, timeoutSeconds, transaction, isIdempotent: true);
        }

        /// <summary>
        /// Creates a new command definition with the specified active transaction.
        /// </summary>
        /// <param name="transaction">The transaction to associate.</param>
        /// <returns>A new <see cref="RelationalCommandDefinition"/> instance.</returns>
        public RelationalCommandDefinition WithTransaction(DbTransaction? transaction)
        {
            return new RelationalCommandDefinition(
                CommandText,
                Parameters,
                CommandType,
                TimeoutSeconds,
                transaction,
                IsIdempotent);
        }

        /// <summary>
        /// Applies the command text, command type, timeout, transaction, and parameterized values to an ADO.NET <see cref="DbCommand"/>.
        /// </summary>
        /// <param name="dbCommand">The ADO.NET command to configure.</param>
        public void ApplyTo(DbCommand dbCommand)
        {
            if (dbCommand == null) throw new ArgumentNullException(nameof(dbCommand));

            dbCommand.CommandText = CommandText;
            dbCommand.CommandType = CommandType;

            if (TimeoutSeconds.HasValue)
            {
                dbCommand.CommandTimeout = TimeoutSeconds.Value;
            }

            if (Transaction != null)
            {
                dbCommand.Transaction = Transaction;
            }

            dbCommand.Parameters.Clear();

            foreach (KeyValuePair<string, object?> pair in Parameters)
            {
                DbParameter parameter = dbCommand.CreateParameter();
                parameter.ParameterName = pair.Key.StartsWith("@", StringComparison.Ordinal) ? pair.Key : "@" + pair.Key;
                parameter.Value = pair.Value ?? DBNull.Value;
                dbCommand.Parameters.Add(parameter);
            }
        }

        private static IReadOnlyDictionary<string, object?> ExtractParameters(object? parameters)
        {
            if (parameters == null)
            {
                return EmptyParameters;
            }

            if (parameters is IReadOnlyDictionary<string, object?> readOnlyDict)
            {
                return readOnlyDict;
            }

            if (parameters is IDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            PropertyInfo[] properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.CanRead)
                {
                    result[property.Name] = property.GetValue(parameters);
                }
            }

            return result;
        }
    }
}
