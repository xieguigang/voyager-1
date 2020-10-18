imports "voyager1" from "voyager";
imports "wav" from "signalKit";

const goldenRecord as string = "J:\GoogleDrive\Voyager\384kHzStereo.wav";

# A demo R# script for image decode from the goden record wave data
# this very first circle image on the goden record is used for 
# parameter calibration of the image decoder
using wav as read.wav(file = file(goldenRecord), lazy = TRUE) {
    # view of the raw file data summary;
    print(wav);

    # parameters of the first circle image
    # and wav decoder arguments
    let first_circle = new image.chunk(channel = "Left", start = 6000208, length = 1928181);
    let decoder = new decode(windowSize = 3400, offset = 217);

    print(first_circle);

    # run decoder and save the
    # result image file
    wav 
    :> decode(chunk = first_circle, decode = decoder)
    :> bitmap(file = `${dirname(!script$dir)}/docs/circle.png`)
    ;
}