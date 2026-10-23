using System.Numerics;

namespace BlochSimulator.Services
{
    /// <summary>Egy önteszt eredménye.</summary>
    public record SelfTestResult(string Name, bool Passed, string Detail);

    /// <summary>
    /// Beépített önteszt: ismert kvantummechanikai azonosságokat ellenőriz a szimulátoron.
    /// A felületen egy kattintással lefuttatható, így a program helyessége bemutatható.
    /// </summary>
    public static class QuantumSelfTest
    {
        private const double Tolerance = 1e-6;
        private const double Pi = Math.PI;

        public static List<SelfTestResult> Run()
        {
            var results = new List<SelfTestResult>();

            void Check(string name, (double x, double y, double z) actual, (double x, double y, double z) expected)
            {
                bool ok = Math.Abs(actual.x - expected.x) < Tolerance
                          && Math.Abs(actual.y - expected.y) < Tolerance
                          && Math.Abs(actual.z - expected.z) < Tolerance;
                results.Add(new SelfTestResult(name, ok,
                    $"kapott ({actual.x:F3}, {actual.y:F3}, {actual.z:F3}) · elvárt ({expected.x:F3}, {expected.y:F3}, {expected.z:F3})"));
            }

            Check("X|0⟩ = |1⟩", Run(0, 0, ("X", 0)), (0, 0, -1));
            Check("H|0⟩ = |+⟩", Run(0, 0, ("H", 0)), (1, 0, 0));
            Check("H|1⟩ = |−⟩", Run(Pi, 0, ("H", 0)), (-1, 0, 0));
            Check("Z|+⟩ = |−⟩", Run(Pi / 2, 0, ("Z", 0)), (-1, 0, 0));
            Check("S|+⟩ = |i⟩", Run(Pi / 2, 0, ("S", 0)), (0, 1, 0));
            Check("S² = Z (S,S|+⟩ → |−⟩)", Run(Pi / 2, 0, ("S", 0), ("S", 0)), (-1, 0, 0));
            Check("T² = S (T,T|+⟩ → |i⟩)", Run(Pi / 2, 0, ("T", 0), ("T", 0)), (0, 1, 0));
            Check("S⁴ = I (S×4|0⟩ → |0⟩)", Run(0, 0, ("S", 0), ("S", 0), ("S", 0), ("S", 0)), (0, 0, 1));
            Check("X·X = I", Run(0, 0, ("X", 0), ("X", 0)), (0, 0, 1));
            Check("Rx(π)|0⟩ = |1⟩", Run(0, 0, ("Rx", Pi)), (0, 0, -1));
            Check("Ry(π/2)|0⟩ = |+⟩", Run(0, 0, ("Ry", Pi / 2)), (1, 0, 0));
            Check("Ry(π)|0⟩ = |1⟩", Run(0, 0, ("Ry", Pi)), (0, 0, -1));
            Check("Rz(π/2)|+⟩ = |i⟩", Run(Pi / 2, 0, ("Rz", Pi / 2)), (0, 1, 0));
            Check("HZH = X", Run(0, 0, ("H", 0), ("Z", 0), ("H", 0)), (0, 0, -1));
            Check("HZH|+⟩ = −|+⟩ (X|+⟩)", Run(Pi / 2, 0, ("H", 0), ("Z", 0), ("H", 0)), (1, 0, 0));
            Check("Rz(2π) = −I (fizikai állapot változatlan)", Run(Pi / 2, 0, ("Rz", 2 * Pi)), (1, 0, 0));

            // Visszavonás (inverz mátrix) helyessége
            var undoService = new QuantumService();
            undoService.ResetState(0, 0, clearTrail: false);
            undoService.ApplyGateNow("H");
            undoService.ApplyGateNow("S");
            undoService.UndoLastApplied();
            undoService.UndoLastApplied();
            Check("Visszavonás: H, S majd 2× undo → |0⟩", undoService.GetBlochVector(undoService.CurrentState), (0, 0, 1));

            var undoT = new QuantumService();
            undoT.ResetState(Pi / 2, 0, clearTrail: false);
            undoT.ApplyGateNow("T");
            undoT.UndoLastApplied();
            Check("T⁻¹ = T† (T után undo → |+⟩)", undoT.GetBlochVector(undoT.CurrentState), (1, 0, 0));

            // Numerikus stabilitás: 40 véletlen kapu után is egységnyi a norma.
            var rng = new Random(2026);
            var service = new QuantumService();
            service.ResetState(0.7, 1.1);
            for (int i = 0; i < 40; i++)
            {
                string[] names = { "X", "Y", "Z", "H", "S", "T", "Rx", "Ry", "Rz" };
                string gate = names[rng.Next(names.Length)];
                double angle = QuantumService.IsRotation(gate) ? rng.NextDouble() * 2 * Pi : 0;
                service.AddGate(gate, angle);
            }
            service.ApplyAll();
            double norm = service.GetNorm();
            results.Add(new SelfTestResult("Normamegmaradás 40 véletlen kapu után", Math.Abs(norm - 1) < 1e-9,
                $"‖ψ‖ = {norm:F12}"));

            return results;
        }

        private static (double x, double y, double z) Run(double theta, double phi,
            params (string gate, double angle)[] gates)
        {
            var service = new QuantumService();
            service.ResetState(theta, phi, clearTrail: false);
            foreach (var (gate, angle) in gates)
            {
                service.AddGate(gate, angle);
            }
            service.ApplyAll();
            return service.GetBlochVector(service.CurrentState);
        }
    }
}
