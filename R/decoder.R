imports "goldenRecord" from "voyager-1";
imports "wav" from "signalKit";

#' Decode a grayscale image on the disk
#' 
#' the scan line period and the pixel clock are measured from the waveform data
#' itself (see the ``decode.report`` function), the legacy ``windowSize`` and
#' ``offset`` parameters of the decoder are not used any more.
#' 
#' @param start the data start position on the raw wav file
#' @param wavFile the file path of the wav file
#' @param size the pixel cells (the image rows) inside a single scan line, default 364
#' @param lines the number of the scan lines (the image columns), default 512
#' @param white the white point gain of the rendering, 1 means no adjustment
#' @param gamma the gamma correction of the rendering, 1 means no correction
#' @param percentile the black point and the white point percentiles of the auto levels
#' @param palette the palette name of the pseudo color rendering
#' @param smooth the radius of the across scan line smoothing, 0 for disable.
#'               the smoothing is disabled by default because it reduces the edge
#'               contrast of the decoded image and makes it look blurred
#' @param luminous render the image as the pseudo color image
#' @param report print the empirical analysis report of the data section
#' 
#' @return a bitmap image object
#' 
let decodeImage = function(start as integer, wavFile as string, 
						   size = 364,
						   lines = 512,
						   white = 1, 
						   gamma = 1,
						   percentile = NULL,
						   palette = "gray",
						   smooth = 0,
						   luminous = FALSE,
						   report = FALSE) {

	using wav as read.wav(file = file(wavFile), lazy = TRUE) {
		# view of the raw file data summary;
		print(wav);

		# the image data section: the calibration circle image is about
		# 1928181 sample frames long (about 5 seconds)
		let chunk   = new image.chunk(channel = "Left", start = start, length = 1928181);
		let decoder = new decode(pixels = size, lines = lines);
		
		print("data size of this image chunk:");
		print(wav |> chunk_size(chunk = chunk));

		if (report) {
			# the empirical analysis of the image data section: the scan line
			# period, the pixel clock, the brightness encoding scheme and the
			# color component detection result
			print(wav |> decode.report(chunk = chunk));
		}

		# run decoder and save the
		# result image file
		wav 
		|> decode(
			chunk  = chunk, 
			decode = decoder
		)
		|> as.bitmap(
			white      = white,
			gamma      = gamma,
			percentile = percentile,
			palette    = palette,
			smooth     = smooth,
			luminous   = luminous
		)
		;
	}
}
