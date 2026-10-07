using System.Globalization;
using System.Numerics;

namespace MarketClock
{
    /// <summary>
    /// Evaluates arithmetic typed in the Notes and Calculator panel, e.g. "(5 / 2) * 340".
    /// Supports + - * / with the usual precedence, % (modulo), ^ (exponent), ! (factorial),
    /// unary minus and parentheses.
    /// Whole numbers are kept exact at any size, so 30! or 2^200 come out with every digit;
    /// answers are always written out in full, never in scientific notation.
    /// </summary>
    public static class ExpressionEvaluator
    {
        private const int MaxFactorial = 1000;   // 1000! has 2,568 digits
        private const int MaxDigits = 5000;      // largest whole number a power may produce

        /// <summary>The answer as text, or "error: ..." when the expression cannot be worked out.</summary>
        public static string EvaluateToText(string expression)
        {
            try
            {
                return new Parser(expression).ParseAll().ToText();
            }
            catch (FormatException ex)
            {
                return "error: " + ex.Message;
            }
        }

        /// <summary>A value that is either an exact whole number or a fraction held as a double.</summary>
        private readonly struct Number
        {
            private readonly BigInteger whole;
            private readonly double fraction;

            public bool IsWhole { get; }

            private Number(BigInteger whole)
            {
                this.whole = whole;
                fraction = 0;
                IsWhole = true;
            }

            private Number(double fraction)
            {
                whole = BigInteger.Zero;
                this.fraction = fraction;
                IsWhole = false;
            }

            public static Number FromWhole(BigInteger value) => new(value);

            /// <summary>A double result; one that happens to be a whole number goes back to being exact.</summary>
            public static Number FromDouble(double value)
            {
                if (double.IsNaN(value))
                {
                    throw new FormatException("result is not a real number");
                }

                if (double.IsInfinity(value))
                {
                    throw new FormatException("result is too large");
                }

                return value == Math.Floor(value) ? new Number(new BigInteger(value)) : new Number(value);
            }

            private double AsDouble => IsWhole ? (double)whole : fraction;

            private bool IsZero => IsWhole && whole.IsZero; // a fraction is never zero

            public Number Negate() => IsWhole ? new Number(-whole) : new Number(-fraction);

            public Number Add(Number other) => IsWhole && other.IsWhole
                ? new Number(whole + other.whole)
                : FromDouble(AsDouble + other.AsDouble);

            public Number Subtract(Number other) => IsWhole && other.IsWhole
                ? new Number(whole - other.whole)
                : FromDouble(AsDouble - other.AsDouble);

            public Number Multiply(Number other) => IsWhole && other.IsWhole
                ? new Number(whole * other.whole)
                : FromDouble(AsDouble * other.AsDouble);

            public Number Divide(Number other)
            {
                if (other.IsZero)
                {
                    throw new FormatException("division by zero");
                }

                if (IsWhole && other.IsWhole)
                {
                    var quotient = BigInteger.DivRem(whole, other.whole, out var remainder);
                    if (remainder.IsZero)
                    {
                        return new Number(quotient);
                    }
                }

                return FromDouble(AsDouble / other.AsDouble);
            }

            public Number Modulo(Number other)
            {
                if (other.IsZero)
                {
                    throw new FormatException("modulo by zero");
                }

                return IsWhole && other.IsWhole
                    ? new Number(BigInteger.Remainder(whole, other.whole))
                    : FromDouble(AsDouble % other.AsDouble);
            }

            public Number Power(Number exponent)
            {
                if (IsWhole && exponent.IsWhole && exponent.whole.Sign >= 0)
                {
                    var size = BigInteger.Abs(whole);

                    if (exponent.whole > int.MaxValue
                        || (size > BigInteger.One && (double)exponent.whole * BigInteger.Log10(size) > MaxDigits))
                    {
                        throw new FormatException($"result is too large (over {MaxDigits} digits)");
                    }

                    return new Number(BigInteger.Pow(whole, (int)exponent.whole));
                }

                if (IsZero)
                {
                    throw new FormatException("division by zero"); // 0 to a negative power
                }

                return FromDouble(Math.Pow(AsDouble, exponent.AsDouble));
            }

            public Number Factorial()
            {
                if (!IsWhole || whole.Sign < 0)
                {
                    throw new FormatException("factorial needs a whole number, 0 or more");
                }

                if (whole > MaxFactorial)
                {
                    throw new FormatException($"factorial is too large ({MaxFactorial}! is the limit)");
                }

                var result = BigInteger.One;
                for (var i = 2; i <= (int)whole; i++)
                {
                    result *= i;
                }

                return new Number(result);
            }

            /// <summary>The value written out in full: every digit of a whole number, plain decimals for a fraction.</summary>
            public string ToText()
            {
                if (IsWhole)
                {
                    return whole.ToString("D", CultureInfo.InvariantCulture);
                }

                // Ten decimal places hides floating-point noise such as 0.1 + 0.2 = 0.30000000000000004.
                // A number below 1 gets extra places for its leading zeros, so 0.000000000001234
                // still shows its digits instead of rounding away to nothing.
                var decimals = 10;
                var size = Math.Abs(fraction);
                if (size < 1)
                {
                    decimals = Math.Min(340, decimals + (int)Math.Ceiling(-Math.Log10(size)));
                }

                var text = fraction.ToString("F" + decimals, CultureInfo.InvariantCulture);
                if (text.Contains('.'))
                {
                    text = text.TrimEnd('0').TrimEnd('.');
                }

                return text == "-0" ? "0" : text;
            }
        }

        // Grammar, loosest binding first:
        //   expression := term   (('+' | '-') term)*
        //   term       := unary  (('*' | '/' | '%') unary)*
        //   unary      := ('-' | '+') unary | power
        //   power      := postfix ('^' unary)?          right-associative: 2^3^2 = 2^9
        //   postfix    := primary '!'*
        //   primary    := number | '(' expression ')'
        private sealed class Parser
        {
            private readonly string text;
            private int position;

            public Parser(string text)
            {
                this.text = text;
            }

            public Number ParseAll()
            {
                SkipSpaces();
                if (position >= text.Length)
                {
                    throw new FormatException("nothing to calculate");
                }

                var value = ParseExpression();

                SkipSpaces();
                if (position < text.Length)
                {
                    throw new FormatException(text[position] == ')'
                        ? "unexpected )"
                        : $"unexpected '{text[position]}'");
                }

                return value;
            }

            private Number ParseExpression()
            {
                var value = ParseTerm();

                while (true)
                {
                    if (TryTake('+'))
                    {
                        value = value.Add(ParseTerm());
                    }
                    else if (TryTake('-'))
                    {
                        value = value.Subtract(ParseTerm());
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private Number ParseTerm()
            {
                var value = ParseUnary();

                while (true)
                {
                    if (TryTake('*'))
                    {
                        value = value.Multiply(ParseUnary());
                    }
                    else if (TryTake('/'))
                    {
                        value = value.Divide(ParseUnary());
                    }
                    else if (TryTake('%'))
                    {
                        value = value.Modulo(ParseUnary());
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private Number ParseUnary()
            {
                if (TryTake('-'))
                {
                    return ParseUnary().Negate();
                }

                if (TryTake('+'))
                {
                    return ParseUnary();
                }

                return ParsePower();
            }

            private Number ParsePower()
            {
                var value = ParsePostfix();

                // The exponent is parsed as a unary so that 2^-1 and 2^3^2 both work,
                // while -2^2 stays -(2^2) because the minus is taken before we get here.
                return TryTake('^') ? value.Power(ParseUnary()) : value;
            }

            private Number ParsePostfix()
            {
                var value = ParsePrimary();

                while (TryTake('!'))
                {
                    value = value.Factorial();
                }

                return value;
            }

            private Number ParsePrimary()
            {
                SkipSpaces();

                if (TryTake('('))
                {
                    var value = ParseExpression();
                    if (!TryTake(')'))
                    {
                        throw new FormatException("missing )");
                    }

                    return value;
                }

                var start = position;
                while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.'))
                {
                    position++;
                }

                if (position == start)
                {
                    throw new FormatException(position >= text.Length
                        ? "expression is incomplete"
                        : $"unexpected '{text[position]}'");
                }

                var number = text[start..position];

                if (!number.Contains('.'))
                {
                    return Number.FromWhole(BigInteger.Parse(number, NumberStyles.None, CultureInfo.InvariantCulture));
                }

                if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new FormatException($"'{number}' is not a number");
                }

                return Number.FromDouble(parsed);
            }

            private bool TryTake(char symbol)
            {
                SkipSpaces();
                if (position < text.Length && text[position] == symbol)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private void SkipSpaces()
            {
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                {
                    position++;
                }
            }
        }
    }
}
