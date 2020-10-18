imports "voyager1" from "voyager";
imports "wav" from "signalKit";

# R script for image parameter calibration

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

using wav as read.wav(file = file(goldenRecord), lazy = TRUE) {
	# view of the raw file data summary;
	print(wav);

	let first_circle = new image.chunk(channel = "Left", start = 6000208, length = 1928181);
	let decoder = new decode(windowSize = 3400, offset = 217);

	print(first_circle);

	wav 
	:> decode(chunk = first_circle, decode = decoder)
	:> bitmap(file = "../docs/circle.png")
	;
}