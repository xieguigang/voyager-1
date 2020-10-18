imports "voyager1" from "voyager";
imports "wav" from "signalKit";

# R script for image parameter calibration

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

let wav = read.wav(file = goldenRecord);

# view of the raw file data summary;
print(wav);