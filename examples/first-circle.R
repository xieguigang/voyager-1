require(voyager1);

imports "goldenRecord" from "voyager-1";
imports "wav" from "signalKit";

const goldenRecord as string = here("../docs/384kHzStereo.wav");

# A demo R# script for image decode from the goden record wave data
# this very first circle image on the goden record is used for 
# parameter calibration of the image decoder
using wav as read.wav(file = file(goldenRecord), lazy = TRUE) {
    # view of the raw file data summary;
    print(wav);

    # parameters of the first circle image
    # and wav decoder arguments
    # 
    # the scan line period and the pixel clock are measured from the waveform data
    # itself, so the legacy windowSize/offset parameters are not used any more;
    # the pixel cells (the image rows) inside a single scan line is 364 for
    # keeping the calibration circle as a true circle
    const first_circle = new image.chunk(
        channel = "Left", 
        start   = 6000208, 
        length  = 1928181
    );
    const decoder = new decode(pixels = 364, lines = 512);

    print(first_circle);
	print("data size of this image chunk:");
	print(wav :> chunk_size(chunk = first_circle));

    # the empirical analysis of the data section: the scan line period, the pixel
    # clock, the brightness encoding scheme and the color component detection
    print(wav |> decode.report(chunk = first_circle));

    # run decoder and save the
    # result image file
    bitmap(file = here("../docs/decoded/circle-r.png")) {
        let decode_data = wav 
        |> decode(
            chunk  = first_circle, 
            decode = decoder
        );

        # export signal intensity scale data into csv file
        write_csv(decode_data, file = here("../docs/decoded/circle-r.csv"));
        
        # rendering as bitmap image in gray scale
        # 
        # the image data of the golden record is stored as a photographic negative,
        # so the black/white polarity is inverted by default; the across scan line
        # smoothing is disabled by default for keeping the image sharp
        decode_data |> as.bitmap() 
        ;
    }
}
