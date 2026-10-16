require(voyager1);

imports "goldenRecord" from "voyager-1";
imports "wav" from "signalKit";

const goldenRecord as string = "../docs/384kHzStereo.wav";

setwd(@dir);

# A demo R# script for export all images 
# in the goden record
# 
# the image data sections are pre-defined in the ``R/chunks.R`` script
# (the same list also can be loaded by the ``test --batch --chunks <file>``
# command line of the ``src/test`` project)
# 
# the images will be exported into the ``docs/decoded`` directory, so it is
# convenient for comparing them with the original decoder result image files
const output_dir as string = "../docs/decoded"; 

goldenRecord
|> export_from_chunks(
    output_dir = output_dir,
    size       = 364,
    lines      = 512,
    smooth     = 0,
    report     = FALSE
)
;
