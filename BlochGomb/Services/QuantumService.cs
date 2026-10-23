using System.Numerics;

namespace BlochSimulator.Services
{
    /// <summary>
    /// Egyqubites kvantumállapot-szimulátor.
    /// A kvantumállapotot komplex amplitúdókkal tárolja, a kapukat 2x2-es unitér
    /// mátrixokkal reprezentálja, és kiszámítja a Bloch-vektort a vizualizációhoz.
    /// </summary>
    public class QuantumService
    {
        // ------------------------------------------------------------------
        // Kapumátrixok (unitér, 2x2)
        // ------------------------------------------------------------------
        private static readonly Complex[,] I = new[,]
        {
            { new Complex(1, 0), new Complex(0, 0) },
            { new Complex(0, 0), new Complex(1, 0) }
        };

        private static readonly Complex[,] X = new[,]
        {
            { new Complex(0, 0), new Complex(1, 0) },
            { new Complex(1, 0), new Complex(0, 0) }
        };

        private static readonly Complex[,] Y = new[,]
        {
            { new Complex(0, 0), new Complex(0, -1) },
            { new Complex(0, 1), new Complex(0, 0) }
        };

        private static readonly Complex[,] Z = new[,]
        {
            { new Complex(1, 0), new Complex(0, 0) },
            { new Complex(0, 0), new Complex(-1, 0) }
        };

        private static readonly Complex[,] H = new[,]
        {
            { new Complex(1 / Math.Sqrt(2), 0), new Complex(1 / Math.Sqrt(2), 0) },
            { new Complex(1 / Math.Sqrt(2), 0), new Complex(-1 / Math.Sqrt(2), 0) }
        };

        private static readonly Complex[,] S = new[,]
        {
            { new Complex(1, 0), new Complex(0, 0) },
            { new Complex(0, 0), new Complex(0, 1) }
        };

        private static readonly Complex[,] T = new[,]
        {
            { new Complex(1, 0), new Complex(0, 0) },
            { new Complex(0, 0), Complex.FromPolarCoordinates(1, Math.PI / 4) }
        };

        // ------------------------------------------------------------------
        // Kapukatalógus a felhasználói felülethez
        // ------------------------------------------------------------------
        public static readonly GateDescriptor[] GateLibrary =
        {
            new("I", "I", "Pauli", "Identitás: nem változtat az állapoton.", "[1 0; 0 1]"),
            new("X", "X", "Pauli", "Bit-flip (NOT): |0⟩ ↔ |1⟩, 180°-os forgatás az X-tengely körül.", "[0 1; 1 0]"),
            new("Y", "Y", "Pauli", "Kombinált bit- és fázisflip, 180°-os forgatás az Y körül.", "[0 -i; i 0]"),
            new("Z", "Z", "Pauli", "Fázisflip: |1⟩ előjelét váltja, 180°-os forgatás a Z körül.", "[1 0; 0 -1]"),
            new("H", "H", "Speciális", "Hadamard: szuperpozíció, a Z-bázist az X-bázisba viszi.", "1/√2 [1 1; 1 -1]"),
            new("S", "S", "Fázis", "Negyedfordulat a Z körül: 90°-os fázisforgatás (√Z).", "[1 0; 0 i]"),
            new("T", "T", "Fázis", "Nyolcadfordulat a Z körül: 45°-os fázisforgatás (√S).", "[1 0; 0 e^(iπ/4)]"),
            new("Rx", "Rx", "Forgatás", "Forgatás az X-tengely körül tetszőleges θ szöggel.", "cos(θ/2)·I − i·sin(θ/2)·X"),
            new("Ry", "Ry", "Forgatás", "Forgatás az Y-tengely körül tetszőleges θ szöggel.", "cos(θ/2)·I − i·sin(θ/2)·Y"),
            new("Rz", "Rz", "Forgatás", "Forgatás a Z-tengely körül tetszőleges θ szöggel (fázis).", "diag(e^(−iθ/2), e^(iθ/2))")
        };

        public static bool IsRotation(string gateName) => gateName is "Rx" or "Ry" or "Rz";

        // ------------------------------------------------------------------
        // Állapot
        // ------------------------------------------------------------------

        /// <summary>Az aktuális állapotvektor: [α, β]. Kezdetben |0⟩.</summary>
        public Complex[] CurrentState { get; set; } = { new Complex(1, 0), new Complex(0, 0) };

        /// <summary>A még végre nem hajtott kapuk sora.</summary>
        public List<GateOperation> GateSequence { get; set; } = new();

        /// <summary>Kézzel mentett állapotok (összehasonlításhoz).</summary>
        public List<StateSnapshot> Snapshots { get; set; } = new();

        /// <summary>Az állapot által bejárt út a gömbön (nyomvonal).</summary>
        public List<BlochPoint> Trail { get; } = new();

        /// <summary>A már végrehajtott kapuk sorrendben (visszavonáshoz és megjelenítéshez).</summary>
        public List<GateOperation> AppliedGates { get; } = new();

        /// <summary>A nyomvonal maximális hossza (teljesítménykorlát).</summary>
        public const int MaxTrailLength = 120;

        // ------------------------------------------------------------------
        // Lekérdezések
        // ------------------------------------------------------------------

        /// <summary>Bloch-koordináták: x = 2Re(αβ*), y = −2Im(αβ*), z = |α|² − |β|².</summary>
        public (double x, double y, double z) GetBlochVector(Complex[] state)
        {
            if (state.Length != 2) throw new ArgumentException("Az állapotvektornak 2 dimenziósnak kell lennie.");

            Complex alpha = state[0];
            Complex beta = state[1];
            Complex alphaBeta = alpha * Complex.Conjugate(beta);

            double x = 2.0 * alphaBeta.Real;
            double y = -2.0 * alphaBeta.Imaginary;
            double z = alpha.Magnitude * alpha.Magnitude - beta.Magnitude * beta.Magnitude;
            return (x, y, z);
        }

        /// <summary>Mérési valószínűségek a Z-bázisban.</summary>
        public (double p0, double p1) GetProbabilities()
        {
            double p0 = CurrentState[0].Magnitude * CurrentState[0].Magnitude;
            double p1 = CurrentState[1].Magnitude * CurrentState[1].Magnitude;
            return (p0, p1);
        }

        /// <summary>A Bloch-gömbön értelmezett θ (0..π) és φ (0..2π) szög.</summary>
        public (double theta, double phi) GetAngles()
        {
            var (x, y, z) = GetBlochVector(CurrentState);
            double r = Math.Sqrt(x * x + y * y);
            double theta = Math.Atan2(r, z);
            double phi = r < 1e-12 ? 0 : NormalizeAngle(Math.Atan2(y, x));
            return (theta, phi);
        }

        /// <summary>Az állapot olvasható alakja: |ψ⟩ = (a+bi)|0⟩ + (c+di)|1⟩</summary>
        public string FormatState()
        {
            var a = CurrentState[0];
            var b = CurrentState[1];
            return $"({FormatComplex(a)})|0⟩ + ({FormatComplex(b)})|1⟩";
        }

        public static string FormatComplex(Complex c)
        {
            double re = Math.Abs(c.Real) < 1e-9 ? 0 : c.Real;
            double im = Math.Abs(c.Imaginary) < 1e-9 ? 0 : c.Imaginary;
            string sign = im < 0 ? "−" : "+";
            return $"{re:F3} {sign} {Math.Abs(im):F3}i";
        }

        // ------------------------------------------------------------------
        // Állapot beállítása / kapuk
        // ------------------------------------------------------------------

        /// <summary>|ψ⟩ = cos(θ/2)|0⟩ + e^(iφ)·sin(θ/2)|1⟩ beállítása.</summary>
        public void ResetState(double theta, double phi, bool clearTrail = true)
        {
            Complex alpha = new(Math.Cos(theta / 2.0), 0);
            Complex beta = Complex.FromPolarCoordinates(Math.Sin(theta / 2.0), phi);

            CurrentState = new[] { alpha, beta };
            GateSequence.Clear();
            AppliedGates.Clear();
            Normalize(CurrentState);

            if (clearTrail)
            {
                Trail.Clear();
                var (x, y, z) = GetBlochVector(CurrentState);
                Trail.Add(new BlochPoint { X = x, Y = y, Z = z });
            }
        }

        public void AddGate(string name, double theta = 0) =>
            GateSequence.Add(new GateOperation { Name = name, Theta = theta });

        public bool RemoveLastGate()
        {
            if (GateSequence.Count == 0) return false;
            GateSequence.RemoveAt(GateSequence.Count - 1);
            return true;
        }

        public bool RemoveGateAt(int index)
        {
            if (index < 0 || index >= GateSequence.Count) return false;
            GateSequence.RemoveAt(index);
            return true;
        }

        public void ClearSequence() => GateSequence.Clear();

        /// <summary>A sor következő kapujának végrehajtása.</summary>
        public bool ApplyNext()
        {
            if (GateSequence.Count == 0) return false;

            var gate = GateSequence[0];
            GateSequence.RemoveAt(0);

            Complex[,] matrix = GetGateMatrix(gate.Name, gate.Theta);
            CurrentState = MultiplyMatrix(matrix, CurrentState);
            Normalize(CurrentState);
            AppliedGates.Add(gate);
            RecordTrailPoint();
            return true;
        }

        /// <summary>Az összes hátralévő kapu végrehajtása; visszaadja a végrehajtott kapuk számát.</summary>
        public int ApplyAll()
        {
            int applied = 0;
            while (ApplyNext()) applied++;
            return applied;
        }

        /// <summary>Egy kapu azonnali alkalmazása (a sor megkerülésével).</summary>
        public void ApplyGateNow(string name, double theta = 0)
        {
            var gate = new GateOperation { Name = name, Theta = theta };
            Complex[,] matrix = GetGateMatrix(name, theta);
            CurrentState = MultiplyMatrix(matrix, CurrentState);
            Normalize(CurrentState);
            AppliedGates.Add(gate);
            RecordTrailPoint();
        }

        /// <summary>
        /// Az utolsó végrehajtott kapu visszavonása az inverz (adjungált) mátrixszal.
        /// Unitér kapuknál U⁻¹ = U†, így a művelet pontosan visszafordítható.
        /// </summary>
        public bool UndoLastApplied()
        {
            if (AppliedGates.Count == 0) return false;

            var gate = AppliedGates[AppliedGates.Count - 1];
            AppliedGates.RemoveAt(AppliedGates.Count - 1);

            Complex[,] inverse = GetInverseMatrix(gate.Name, gate.Theta);
            CurrentState = MultiplyMatrix(inverse, CurrentState);
            Normalize(CurrentState);

            if (Trail.Count > 1) Trail.RemoveAt(Trail.Count - 1);
            return true;
        }

        /// <summary>Z-bázisú mérés szimulációja: a hullámfüggvény összeomlik.</summary>
        public (int result, double p0, double p1) MeasureZ(Random rng)
        {
            var (p0, p1) = GetProbabilities();
            int result = rng.NextDouble() < p0 ? 0 : 1;
            CurrentState = result == 0
                ? new[] { new Complex(1, 0), new Complex(0, 0) }
                : new[] { new Complex(0, 0), new Complex(1, 0) };
            // a mérés nem unitér, ezért a kapu-előzmény érvényét veszti
            AppliedGates.Clear();
            RecordTrailPoint();
            return (result, p0, p1);
        }

        // ------------------------------------------------------------------
        // Pillanatképek és nyomvonal
        // ------------------------------------------------------------------

        public void SaveSnapshot(string label, string color)
        {
            var (x, y, z) = GetBlochVector(CurrentState);
            Snapshots.Add(new StateSnapshot
            {
                Label = string.IsNullOrWhiteSpace(label) ? $"Állapot {Snapshots.Count + 1}" : label.Trim(),
                Color = string.IsNullOrWhiteSpace(color) ? "#f59e0b" : color,
                State = (Complex[])CurrentState.Clone(),
                X = x, Y = y, Z = z
            });
        }

        public bool RemoveSnapshot(StateSnapshot snapshot) => Snapshots.Remove(snapshot);

        public void ClearSnapshots() => Snapshots.Clear();

        public void ClearTrail()
        {
            Trail.Clear();
            var (x, y, z) = GetBlochVector(CurrentState);
            Trail.Add(new BlochPoint { X = x, Y = y, Z = z });
        }

        private void RecordTrailPoint()
        {
            var (x, y, z) = GetBlochVector(CurrentState);
            Trail.Add(new BlochPoint { X = x, Y = y, Z = z });
            if (Trail.Count > MaxTrailLength)
            {
                Trail.RemoveAt(0);
            }
        }

        // ------------------------------------------------------------------
        // Segédfüggvények
        // ------------------------------------------------------------------

        private Complex[,] GetGateMatrix(string name, double theta) => name switch
        {
            "I" => I,
            "X" => X,
            "Y" => Y,
            "Z" => Z,
            "H" => H,
            "S" => S,
            "T" => T,
            "Rx" => GetRx(theta),
            "Ry" => GetRy(theta),
            "Rz" => GetRz(theta),
            _ => throw new ArgumentException($"Ismeretlen kapu: {name}")
        };

        /// <summary>Unitér kapu inverze (U†). Önadjungált kapuknál önmaga.</summary>
        private Complex[,] GetInverseMatrix(string name, double theta) => name switch
        {
            "I" => I,
            "X" => X,
            "Y" => Y,
            "Z" => Z,
            "H" => H,
            "S" => new[,]
            {
                { new Complex(1, 0), new Complex(0, 0) },
                { new Complex(0, 0), new Complex(0, -1) }
            },
            "T" => new[,]
            {
                { new Complex(1, 0), new Complex(0, 0) },
                { new Complex(0, 0), Complex.FromPolarCoordinates(1, -Math.PI / 4) }
            },
            "Rx" => GetRx(-theta),
            "Ry" => GetRy(-theta),
            "Rz" => GetRz(-theta),
            _ => throw new ArgumentException($"Ismeretlen kapu: {name}")
        };

        private static Complex[,] GetRx(double theta)
        {
            double c = Math.Cos(theta / 2.0);
            double s = Math.Sin(theta / 2.0);
            return new[,]
            {
                { new Complex(c, 0), new Complex(0, -s) },
                { new Complex(0, -s), new Complex(c, 0) }
            };
        }

        private static Complex[,] GetRy(double theta)
        {
            double c = Math.Cos(theta / 2.0);
            double s = Math.Sin(theta / 2.0);
            return new[,]
            {
                { new Complex(c, 0), new Complex(-s, 0) },
                { new Complex(s, 0), new Complex(c, 0) }
            };
        }

        private static Complex[,] GetRz(double theta) => new[,]
        {
            { Complex.FromPolarCoordinates(1, -theta / 2.0), new Complex(0, 0) },
            { new Complex(0, 0), Complex.FromPolarCoordinates(1, theta / 2.0) }
        };

        private static Complex[] MultiplyMatrix(Complex[,] matrix, Complex[] vector) => new[]
        {
            matrix[0, 0] * vector[0] + matrix[0, 1] * vector[1],
            matrix[1, 0] * vector[0] + matrix[1, 1] * vector[1]
        };

        private static void Normalize(Complex[] state)
        {
            double norm = Math.Sqrt(state[0].Magnitude * state[0].Magnitude +
                                    state[1].Magnitude * state[1].Magnitude);
            if (norm > 0 && Math.Abs(norm - 1.0) > 1e-12)
            {
                state[0] /= norm;
                state[1] /= norm;
            }
        }

        private static double NormalizeAngle(double angle)
        {
            double twoPi = 2 * Math.PI;
            angle %= twoPi;
            if (angle < 0) angle += twoPi;
            return angle;
        }

        /// <summary>Norma-ellenőrzés a numerikus stabilitás igazolásához.</summary>
        public double GetNorm() =>
            Math.Sqrt(CurrentState[0].Magnitude * CurrentState[0].Magnitude +
                      CurrentState[1].Magnitude * CurrentState[1].Magnitude);
    }

    /// <summary>Egy kapu a végrehajtási sorban.</summary>
    public class GateOperation
    {
        public string Name { get; set; } = "";
        public double Theta { get; set; }

        public string Label => Math.Abs(Theta) > 1e-4 ? $"{Name}({Theta:0.###})" : Name;
    }

    /// <summary>Egy mentett állapot a Bloch-gömbön való összehasonlításhoz.</summary>
    public class StateSnapshot
    {
        public string Label { get; set; } = "";
        public string Color { get; set; } = "#f59e0b";
        public Complex[] State { get; set; } = Array.Empty<Complex>();
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>Egy pont a Bloch-gömbön (a nyomvonalhoz).</summary>
    public class BlochPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>Kapu leíró a felhasználói felülethez.</summary>
    public record GateDescriptor(string Name, string Symbol, string Category, string Description, string Matrix);
}

