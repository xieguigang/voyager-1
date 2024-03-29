imports "goldenRecord" from "voyager-1";
imports "wav" from "signalKit";

# const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

#' Decode a grayscale image on the disk
#' 
#' @param start the data start position on the raw wav file
#' @param wavFile the file path of the wav file
#' 
#' @return a bitmap image object
#' 
let decodeImage = function(start as integer, wavFile as string, offsetLeft = 0.15, offsetRight = 0.1, white = 0.8, luminous = FALSE) {
	using wav as read.wav(file = file(wavFile), lazy = TRUE) {
		# view of the raw file data summary;
		print(wav);

		# parameters of the image
		# and wav decoder arguments
		let chunk = new image.chunk(channel = "Left", start = start, length = 1800000);
		let decoder = new decode(windowSize = 3400, offset = 384);
		
		print("data size of this image chunk:");
		print(wav :> chunk_size(chunk = chunk));

		# run decoder and save the
		# result image file
		wav 
		:> decode(
			chunk       = chunk, 
			decode      = decoder, 
			offsetLeft  = offsetLeft, 
			offsetRight = offsetRight
		)
		:> as.bitmap(white = white, luminous = luminous)
		;
	}
}