namespace TransjapHorimetros.Application.Exceptions;

public sealed class PayloadTooLargeException(string message) : Exception(message);
