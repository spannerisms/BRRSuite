# WaveContainer

The `WaveContainer` class provides a simple container for the [Microsoft Wave Sound File format](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/extensible-wave-format-descriptors).

----

## Constructors

| Constructor | Description |
| ----------- | ----------- |
| <samp>WaveContainer(int, int, int)</samp> | Initializes a new instance with a given sample rate, bit-depth, and sample count.
| <samp>WaveContainer(Stream)</samp> | Initializes a new instance from a stream containing a valid Wave Sound file.

----

## Properties

| Property | Access | Type | Description |
| --------:|:------:|:----:| ----------- |
| <samp>Item[Index]</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>short</kbd> | Accesses the sample data.
| <samp>SampleRate</samp> | <kbd>get</kbd> | <kbd>SampleRate</kbd> | The playback frequency of the audio.
| <samp>BitsPerSample</samp> | <kbd>get</kbd> | <kbd>int</kbd> | The bit depth of one sample.
| <samp>BytesPerSample</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Number of bytes required to contain one sample.
| <samp>SampleCount</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Number of samples in the audio.
| <samp>Channels</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Number of individual audio channels.
| <samp>ByteRate</samp> | <kbd>get</kbd> | <kbd>int</kbd> | The number of bytes processed per second when playing back at 100% speed.
| <samp>Chunk2Size</samp> | <kbd>get</kbd> | <kbd>int</kbd> | The size of the samples data in bytes.

----

## Instance methods

| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>SamplesToArray()</samp> | <kbd>int[]</kbd> | Creates a new array containing a copy of the samples data as signed, 32-bit integers.
| <samp>AsMemoryStream()</samp> | <kbd>MemoryStream</kbd> | Creates a readonly stream over the entire data, including header.
| <samp>AsSpan()</samp> | <kbd>Span&lt;byte&gt;</kbd> | Creates a span over the entire data, including header.
| <samp>SamplesAsSpan()</samp> | <kbd>Span&lt;short&gt;</kbd> | Creates a span over the samples data
| <samp>Save(string, bool)</samp> | <kbd>void</kbd> | Saves the audio file to given path.

----

## Constant fields
| Method | Type | Value | Description |
| ------ |:----:| ----- | ----------- |
| <samp>Extension</samp> | <kbd>string</kbd> | `"wav"` | The preferred extension for Wave Sound files.

----

## Static methods

| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>VerifyWAV(byte[], out&nbsp;string)</samp> | <kbd>bool</kbd> | Validates a block of data for Wave Sound file validity.