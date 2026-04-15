using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CrystalMagic.Core
{
    [Serializable]
    public class SaveVariableData
    {
        [SerializeField] private List<SaveVariableEntry> entries = new();

        [NonSerialized] private Dictionary<string, double> _cache;

        public void Set(string key, double value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Save variable key cannot be null or empty.", nameof(key));
            }

            Dictionary<string, double> cache = GetCache();
            cache[key] = value;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key != key)
                {
                    continue;
                }

                entries[i].Value = value;
                return;
            }

            entries.Add(new SaveVariableEntry
            {
                Key = key,
                Value = value,
            });
        }

        public double Get(string key, double defaultValue = 0d)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            return GetCache().TryGetValue(key, out double value) ? value : defaultValue;
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return GetCache().ContainsKey(key);
        }

        public bool Check(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                List<Token> tokens = Tokenize(expression);
                List<Token> postfix = ToPostfix(tokens);
                double result = Evaluate(postfix, GetCache());
                return ToBool(result);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveVariableData] Failed to check expression '{expression}': {ex.Message}");
                return false;
            }
        }

        private Dictionary<string, double> GetCache()
        {
            if (_cache != null)
            {
                return _cache;
            }

            _cache = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                SaveVariableEntry entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                _cache[entry.Key] = entry.Value;
            }

            return _cache;
        }

        private static List<Token> Tokenize(string expression)
        {
            List<Token> tokens = new();
            int index = 0;

            while (index < expression.Length)
            {
                char current = expression[index];

                if (char.IsWhiteSpace(current))
                {
                    index++;
                    continue;
                }

                if (current == '(')
                {
                    tokens.Add(Token.LeftParen());
                    index++;
                    continue;
                }

                if (current == ')')
                {
                    tokens.Add(Token.RightParen());
                    index++;
                    continue;
                }

                if (char.IsDigit(current) || current == '.')
                {
                    int start = index;
                    index++;
                    while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                    {
                        index++;
                    }

                    string literal = expression.Substring(start, index - start);
                    tokens.Add(Token.Number(double.Parse(literal, CultureInfo.InvariantCulture)));
                    continue;
                }

                if (char.IsLetter(current) || current == '_')
                {
                    int start = index;
                    index++;
                    while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
                    {
                        index++;
                    }

                    tokens.Add(Token.Identifier(expression.Substring(start, index - start)));
                    continue;
                }

                string op = ReadOperator(expression, ref index, tokens);
                tokens.Add(Token.Operator(op));
            }

            return tokens;
        }

        private static string ReadOperator(string expression, ref int index, List<Token> tokens)
        {
            if (index + 1 < expression.Length)
            {
                string twoChars = expression.Substring(index, 2);
                if (twoChars == ">=" || twoChars == "<=" || twoChars == "==" || twoChars == "!=" || twoChars == "&&" || twoChars == "||")
                {
                    index += 2;
                    return twoChars;
                }
            }

            char current = expression[index];
            index++;

            if (current == '-')
            {
                bool isUnary = tokens.Count == 0 ||
                               tokens[tokens.Count - 1].Type == TokenType.Operator ||
                               tokens[tokens.Count - 1].Type == TokenType.LeftParen;
                return isUnary ? "u-" : "-";
            }

            if (current == '+')
            {
                bool isUnary = tokens.Count == 0 ||
                               tokens[tokens.Count - 1].Type == TokenType.Operator ||
                               tokens[tokens.Count - 1].Type == TokenType.LeftParen;
                return isUnary ? "u+" : "+";
            }

            if (current == '=')
            {
                return "==";
            }

            if (current == '!' || current == '*' || current == '/' || current == '%' || current == '>' || current == '<')
            {
                return current.ToString();
            }

            throw new InvalidOperationException($"Unsupported operator '{current}'.");
        }

        private static List<Token> ToPostfix(List<Token> tokens)
        {
            List<Token> output = new();
            Stack<Token> operators = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (token.Type == TokenType.Number || token.Type == TokenType.Identifier)
                {
                    output.Add(token);
                    continue;
                }

                if (token.Type == TokenType.Operator)
                {
                    while (operators.Count > 0 &&
                           operators.Peek().Type == TokenType.Operator &&
                           ShouldPopOperator(token.OperatorValue, operators.Peek().OperatorValue))
                    {
                        output.Add(operators.Pop());
                    }

                    operators.Push(token);
                    continue;
                }

                if (token.Type == TokenType.LeftParen)
                {
                    operators.Push(token);
                    continue;
                }

                if (token.Type == TokenType.RightParen)
                {
                    while (operators.Count > 0 && operators.Peek().Type != TokenType.LeftParen)
                    {
                        output.Add(operators.Pop());
                    }

                    if (operators.Count == 0 || operators.Peek().Type != TokenType.LeftParen)
                    {
                        throw new InvalidOperationException("Mismatched parentheses.");
                    }

                    operators.Pop();
                }
            }

            while (operators.Count > 0)
            {
                Token token = operators.Pop();
                if (token.Type == TokenType.LeftParen || token.Type == TokenType.RightParen)
                {
                    throw new InvalidOperationException("Mismatched parentheses.");
                }

                output.Add(token);
            }

            return output;
        }

        private static bool ShouldPopOperator(string current, string top)
        {
            int currentPrecedence = GetPrecedence(current);
            int topPrecedence = GetPrecedence(top);
            if (IsRightAssociative(current))
            {
                return currentPrecedence < topPrecedence;
            }

            return currentPrecedence <= topPrecedence;
        }

        private static int GetPrecedence(string op)
        {
            switch (op)
            {
                case "!":
                case "u-":
                case "u+":
                    return 6;
                case "*":
                case "/":
                case "%":
                    return 5;
                case "+":
                case "-":
                    return 4;
                case ">":
                case ">=":
                case "<":
                case "<=":
                    return 3;
                case "==":
                case "!=":
                    return 2;
                case "&&":
                    return 1;
                case "||":
                    return 0;
                default:
                    throw new InvalidOperationException($"Unknown operator '{op}'.");
            }
        }

        private static bool IsRightAssociative(string op)
        {
            return op == "!" || op == "u-" || op == "u+";
        }

        private static double Evaluate(List<Token> postfix, Dictionary<string, double> values)
        {
            Stack<double> stack = new();

            for (int i = 0; i < postfix.Count; i++)
            {
                Token token = postfix[i];
                if (token.Type == TokenType.Number)
                {
                    stack.Push(token.NumberValue);
                    continue;
                }

                if (token.Type == TokenType.Identifier)
                {
                    stack.Push(values.TryGetValue(token.IdentifierValue, out double value) ? value : 0d);
                    continue;
                }

                if (token.OperatorValue == "!" || token.OperatorValue == "u-" || token.OperatorValue == "u+")
                {
                    if (stack.Count < 1)
                    {
                        throw new InvalidOperationException("Invalid unary expression.");
                    }

                    double operand = stack.Pop();
                    stack.Push(EvaluateUnary(token.OperatorValue, operand));
                    continue;
                }

                if (stack.Count < 2)
                {
                    throw new InvalidOperationException("Invalid binary expression.");
                }

                double right = stack.Pop();
                double left = stack.Pop();
                stack.Push(EvaluateBinary(token.OperatorValue, left, right));
            }

            if (stack.Count != 1)
            {
                throw new InvalidOperationException("Expression evaluation failed.");
            }

            return stack.Pop();
        }

        private static double EvaluateUnary(string op, double operand)
        {
            switch (op)
            {
                case "!":
                    return ToBool(operand) ? 0d : 1d;
                case "u-":
                    return -operand;
                case "u+":
                    return operand;
                default:
                    throw new InvalidOperationException($"Unknown unary operator '{op}'.");
            }
        }

        private static double EvaluateBinary(string op, double left, double right)
        {
            switch (op)
            {
                case "+":
                    return left + right;
                case "-":
                    return left - right;
                case "*":
                    return left * right;
                case "/":
                    return left / right;
                case "%":
                    return left % right;
                case ">":
                    return left > right ? 1d : 0d;
                case ">=":
                    return left >= right ? 1d : 0d;
                case "<":
                    return left < right ? 1d : 0d;
                case "<=":
                    return left <= right ? 1d : 0d;
                case "==":
                    return Math.Abs(left - right) < 0.000001d ? 1d : 0d;
                case "!=":
                    return Math.Abs(left - right) < 0.000001d ? 0d : 1d;
                case "&&":
                    return ToBool(left) && ToBool(right) ? 1d : 0d;
                case "||":
                    return ToBool(left) || ToBool(right) ? 1d : 0d;
                default:
                    throw new InvalidOperationException($"Unknown binary operator '{op}'.");
            }
        }

        private static bool ToBool(double value)
        {
            return Math.Abs(value) > 0.000001d;
        }

        private enum TokenType
        {
            Number,
            Identifier,
            Operator,
            LeftParen,
            RightParen
        }

        private readonly struct Token
        {
            public TokenType Type { get; }
            public double NumberValue { get; }
            public string IdentifierValue { get; }
            public string OperatorValue { get; }

            private Token(TokenType type, double numberValue, string identifierValue, string operatorValue)
            {
                Type = type;
                NumberValue = numberValue;
                IdentifierValue = identifierValue;
                OperatorValue = operatorValue;
            }

            public static Token Number(double value)
            {
                return new Token(TokenType.Number, value, null, null);
            }

            public static Token Identifier(string value)
            {
                return new Token(TokenType.Identifier, 0d, value, null);
            }

            public static Token Operator(string value)
            {
                return new Token(TokenType.Operator, 0d, null, value);
            }

            public static Token LeftParen()
            {
                return new Token(TokenType.LeftParen, 0d, null, null);
            }

            public static Token RightParen()
            {
                return new Token(TokenType.RightParen, 0d, null, null);
            }
        }
    }

    [Serializable]
    public class SaveVariableEntry
    {
        public string Key;
        public double Value;
    }
}
