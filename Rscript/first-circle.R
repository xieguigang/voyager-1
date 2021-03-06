require(voyager1);

imports "goldenRecord" from "voyager";
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
    const first_circle = new image.chunk(
        channel = "Left", 
        start   = 6000000, 
        length  = 1800000
    );
    const decoder = new decode(windowSize = 3400, offset = 384);

    print(first_circle);
	print("data size of this image chunk:");
	print(wav :> chunk_size(chunk = first_circle));

    # run decoder and save the
    # result image file
    bitmap(file = `${dirname(!script$dir)}/docs/circle.png`) {
        wav 
        |> decode(
            chunk       = first_circle, 
            decode      = decoder, 
            offsetLeft  = 0.15, 
            offsetRight = 0.1
        )
        |> as.bitmap() 
        ;
    }
}