imports "voyager1" from "voyager";
imports "wav" from "signalKit";

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

# A demo R# script for export all images 
# in the goden record 
using wav as read.wav(file = file(goldenRecord), lazy = TRUE) {
    # parameters of the first circle image
    # and wav decoder arguments
    let image_chunk = new image.chunk(channel = "Left", start = 6000208, length = 1928181);
    let decoder = new decode(windowSize = 3400, offset = 217);

	# try to decode all images in the wav files
	# its left channel
	for(index in 1:10000) {
		# run decoder and save the
		# result image file
		wav 
		:> decode(chunk = image_chunk, decode = decoder, size = 384)
		:> as.bitmap(white = 0)
		:> bitmap(file = `./test/${index}.png`)
		;		
		
		print(index);
		
		image_chunk = new image.chunk(
			channel = "Left", 
			start   = as.object(image_chunk)$start + 200281, 
			length  = 1928181
		);
	}
}