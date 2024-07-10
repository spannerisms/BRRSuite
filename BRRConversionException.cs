// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// The exception that is thrown when a problem occurs preventing the proper encoding or decoding of BRR or audio data.
/// </summary>
public sealed class BRRConversionException : Exception {
	/// <summary>
	/// Creates a new <see cref="BRRConversionException"/> with the specified message.
	/// </summary>
	/// <inheritdoc cref="Exception(string?)" path="/param"/>
	internal BRRConversionException(string? message) : base(message) { }
}
