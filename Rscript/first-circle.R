imports "voyager1" from "voyager";
imports "wav" from "signalKit";

# R script for image parameter calibration

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

let wav = read.wav(file = file(goldenRecord), lazy = TRUE);

# view of the raw file data summary;
print(wav);

let first_circle = new image.chunk(channel = "Left", start = 6000208, length = 1928181);

print(first_circle);

wav 
:> decode(chunk = first_circle)
:> bitmap(file = "../docs/circle.png")
;