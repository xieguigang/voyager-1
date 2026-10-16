#' A pre-defined list of the image locations
const data_chunks = list(
    "Calibration_circle"        = 6000208,
    "Solar_location_map"        = 8465560,
    "Mathematical_definitions"  = 10686671,
    "Physical_unit_definitions" = 13001000
);

#' the frame length of an image data section, the calibration circle image
#' section is about 1928181 sample frames long
const image_section_length = 1928181;

#' the pixel cells (the image rows) inside a single scan line
const image_pixel_size = 364;

#' the number of the scan lines (the image columns) of a single image
const image_scan_lines = 512;

#' make batch export of the images on the disk
#' 
#' @param wavFile the file path to the disk image in wave file
#' @param output_dir the directory path for export the image files
#' @param size the pixel cells (the image rows) inside a single scan line
#' @param lines the number of the scan lines (the image columns)
#' @param smooth the radius of the across scan line smoothing
#' @param report print the empirical analysis report of every image section
#' 
const export_from_chunks = function(wavFile as string, output_dir as string,
                                    size = image_pixel_size,
                                    lines = image_scan_lines,
                                    smooth = 0,
                                    report = FALSE) {
	using wav as read.wav(file = file(wavFile), lazy = TRUE) {
		# view of the raw file data summary;
		print(wav);

        for(name in names(data_chunks)) {
            bitmap(file = `${output_dir}/${name}.png`) {
                wav |> .export_chunk(name, size = size, lines = lines, smooth = smooth, report = report);
            }
            ;
        }
	}
}

const .export_chunk = function(wav, chunk_name,
                               size = image_pixel_size,
                               lines = image_scan_lines,
                               smooth = 0,
                               report = FALSE) {
    # parameters of the image
    # and wav decoder arguments
    let chunk   = new image.chunk(channel = "Left", start = data_chunks[[chunk_name]], length = image_section_length);
    let decoder = new decode(pixels = size, lines = lines);
    
    print("data size of this image chunk:");
    print(wav |> chunk_size(chunk = chunk));

    if (report) {
        print(wav |> decode.report(chunk = chunk));
    }

    # run decoder and save the
    # result image file
    wav 
    |> decode(
        chunk  = chunk, 
        decode = decoder
    )
    |> as.bitmap(smooth = smooth)
    ;
}
