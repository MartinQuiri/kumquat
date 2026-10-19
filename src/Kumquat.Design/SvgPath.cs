// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Kumquat contributors

using System.Globalization;

namespace Kumquat.Design;

/// <summary>
/// Intérprete del atributo <c>d</c> de SVG. Reduce todo a líneas y Béziers cúbicas (las
/// cuadráticas y los arcos se convierten), que GDI+ y WPF dibujan igual: así el ícono se ve
/// idéntico en la rueda y en las ventanas.
/// </summary>
public static class SvgPath
{
    /// <summary>Interpreta un trazado. Lanza <see cref="FormatException"/> si es inválido.</summary>
    public static IReadOnlyList<PathFigure> Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new Parser(data).Run();
    }

    private sealed class Parser(string data)
    {
        private readonly List<PathFigure> _figures = [];
        private List<PathSegment> _segments = [];
        private PathPoint _start;
        private PathPoint _current;
        private PathPoint _lastControl;
        private char _lastCommand;
        private int _pos;
        private bool _open;

        public List<PathFigure> Run()
        {
            char command = '\0';
            while (true)
            {
                SkipSeparators();
                if (_pos >= data.Length)
                {
                    break;
                }

                if (char.IsLetter(data[_pos]))
                {
                    command = data[_pos++];
                }
                else if (command == '\0')
                {
                    throw new FormatException($"Trazado sin comando en la posición {_pos}.");
                }
                else if (command is 'M' or 'm')
                {
                    // Coordenadas extra tras un "M" son "L" implícitos.
                    command = command == 'M' ? 'L' : 'l';
                }

                Execute(command);
            }

            Flush(closed: false);
            return _figures;
        }

        private void Execute(char command)
        {
            var relative = char.IsLower(command);
            var origin = relative ? _current : default;
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    Flush(closed: false);
                    _current = _start = Point(origin);
                    _open = true;
                    break;
                case 'L':
                    LineTo(Point(origin));
                    break;
                case 'H':
                    LineTo(new PathPoint(Number() + origin.X, _current.Y));
                    break;
                case 'V':
                    LineTo(new PathPoint(_current.X, Number() + origin.Y));
                    break;
                case 'C':
                    CubicTo(Point(origin), Point(origin), Point(origin));
                    break;
                case 'S':
                    CubicTo(Reflected("CS"), Point(origin), Point(origin));
                    break;
                case 'Q':
                    QuadTo(Point(origin), Point(origin));
                    break;
                case 'T':
                    QuadTo(Reflected("QT"), Point(origin));
                    break;
                case 'A':
                    ArcTo(Number(), Number(), Number(), Flag(), Flag(), Point(origin));
                    break;
                case 'Z':
                    _current = _start;
                    Flush(closed: true);
                    break;
                default:
                    throw new FormatException($"Comando de trazado no admitido: {command}.");
            }

            _lastCommand = char.ToUpperInvariant(command);
        }

        private PathPoint Reflected(string kinds)
            => kinds.Contains(_lastCommand, StringComparison.Ordinal)
                ? new PathPoint((2 * _current.X) - _lastControl.X, (2 * _current.Y) - _lastControl.Y)
                : _current;

        private void LineTo(PathPoint to)
        {
            EnsureOpen();
            _segments.Add(PathSegment.Line(to));
            _current = to;
        }

        private void CubicTo(PathPoint c1, PathPoint c2, PathPoint to)
        {
            EnsureOpen();
            _segments.Add(PathSegment.Cubic(c1, c2, to));
            _lastControl = c2;
            _current = to;
        }

        private void QuadTo(PathPoint control, PathPoint to)
        {
            var from = _current;
            CubicTo(
                new PathPoint(from.X + (2.0 / 3 * (control.X - from.X)), from.Y + (2.0 / 3 * (control.Y - from.Y))),
                new PathPoint(to.X + (2.0 / 3 * (control.X - to.X)), to.Y + (2.0 / 3 * (control.Y - to.Y))),
                to);
            _lastControl = control;
        }

        private void ArcTo(double rx, double ry, double rotationDegrees, bool largeArc, bool sweep, PathPoint to)
        {
            EnsureOpen();
            foreach (var segment in ArcToCubics.Convert(_current, rx, ry, rotationDegrees, largeArc, sweep, to))
            {
                _segments.Add(segment);
            }

            _current = to;
        }

        private void EnsureOpen()
        {
            if (!_open)
            {
                // Un dibujo tras "Z" sin "M" arranca en el punto inicial de la figura anterior.
                _start = _current;
                _open = true;
            }
        }

        private void Flush(bool closed)
        {
            if (_open && _segments.Count > 0)
            {
                _figures.Add(new PathFigure(_start, _segments, closed));
            }

            _segments = [];
            _open = false;
        }

        private PathPoint Point(PathPoint origin) => new(Number() + origin.X, Number() + origin.Y);

        private bool Flag()
        {
            SkipSeparators();
            // Las banderas de arco son un solo dígito y pueden ir pegadas ("a1,1,0,011,1").
            if (_pos < data.Length && data[_pos] is '0' or '1')
            {
                return data[_pos++] == '1';
            }

            throw new FormatException($"Se esperaba una bandera 0/1 en la posición {_pos}.");
        }

        private double Number()
        {
            SkipSeparators();
            var begin = _pos;
            if (_pos < data.Length && data[_pos] is '+' or '-')
            {
                _pos++;
            }

            var dot = false;
            while (_pos < data.Length && (char.IsAsciiDigit(data[_pos]) || (data[_pos] == '.' && !dot)))
            {
                dot |= data[_pos] == '.';
                _pos++;
            }

            if (_pos < data.Length && data[_pos] is 'e' or 'E' && _pos + 1 < data.Length && (char.IsAsciiDigit(data[_pos + 1]) || data[_pos + 1] is '-' or '+'))
            {
                _pos += 2;
                while (_pos < data.Length && char.IsAsciiDigit(data[_pos]))
                {
                    _pos++;
                }
            }

            if (!double.TryParse(data.AsSpan(begin, _pos - begin), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Número inválido en la posición {begin}.");
            }

            return value;
        }

        private void SkipSeparators()
        {
            while (_pos < data.Length && (data[_pos] == ',' || char.IsWhiteSpace(data[_pos])))
            {
                _pos++;
            }
        }
    }
}
