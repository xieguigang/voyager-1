require(voyager1);

# raw wav data
const goldenRecord as string = "J:/GoogleDrive/Voyager/384kHzStereo.wav";
const start as integer = ?"--start" || stop("no start position was provided!");
const demo_saveImg as string = `${!script$dir}/export.png`;

decodeImage(start, wavFile = goldenRecord, offsetLeft = 0.2, offsetRight = 0.2, luminous = TRUE) 		
:> bitmap(file = demo_saveImg)
;