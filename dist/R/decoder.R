imports "voyager1" from "voyager";
imports "wav" from "signalKit";

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

let decodeImage as function(start as integer, wavFile as string = goldenRecord, saveImg = "./img.png") {
	using wav as read.wav(file = file(wavFile), lazy = TRUE) {
		# view of the raw file data summary;
		print(wav);

		# parameters of the image
		# and wav decoder arguments
		let chunk = new image.chunk(channel = "Left", start = start, length = 1928181);
		let decoder = new decode(windowSize = 3400, offset = 217);
		
		print("data size of this image chunk:");
		print(wav :> chunk_size(chunk = chunk));

		# run decoder and save the
		# result image file
		wav 
		:> decode(chunk = chunk, decode = decoder)
		:> as.bitmap(white = 1.125)
		:> bitmap(file = saveImg)
		;
	}
}