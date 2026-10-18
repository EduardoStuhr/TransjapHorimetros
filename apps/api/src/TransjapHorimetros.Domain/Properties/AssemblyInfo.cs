using System.Runtime.CompilerServices;

// Legacy fixtures can represent incomplete historical rows. Production callers use CreateOperational.
[assembly: InternalsVisibleTo("TransjapHorimetros.UnitTests")]
[assembly: InternalsVisibleTo("TransjapHorimetros.IntegrationTests")]
