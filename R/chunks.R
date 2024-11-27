#' A pre-defined list of the image locations
const data_chunks = list(
    "Calibration_circle"        = 6000208,
    "Solar_location_map"        = 8465560,
    "Mathematical_definitions"  = 10686671,
    "Physical_unit_definitions" = 13001000
);

#' make batch export of the images on the disk
#' 
#' @param wavFile the file path to the disk image in wave file
#' @param output_dir the directory path for export the image files
#' 
const export_from_chunks = function(wavFile as string, output_dir as string) {
	using wav as read.wav(file = file(wavFile), lazy = TRUE) {
		# view of the raw file data summary;
		print(wav);

        for(name in names(data_chunks)) {
            bitmap(file = `${output_dir}/${name}.png`) {
                wav |> .export_chunk(chunk_name);
            }
            ;
        }
	}
}

const .export_chunk = function(wav, chunk_name) {
    # parameters of the image
    # and wav decoder arguments
    let chunk = new image.chunk(channel = "Left", start = data_chunks[[chunk_name]], length = 1800000);
    let decoder = new decode(windowSize = 3400, offset = 384);
    
    print("data size of this image chunk:");
    print(wav |> chunk_size(chunk = chunk));

    # run decoder and save the
    # result image file
    wav 
    |> decode(
        chunk       = chunk, 
        decode      = decoder, 
        offsetLeft  = 0.2, 
        offsetRight = 0.2
    )
    |> as.bitmap()
    ;
}