# R# first-circle.R --attach voyager1_1.0.0.1254.zip

require(voyager1);

# raw wav data
const goldenRecord as string = "J:/GoogleDrive/Voyager/384kHzStereo.wav";
const demo_saveImg as string = `${!script$dir}/circle.png`;

decodeImage(6000208, wavFile = goldenRecord, offsetLeft = 0.2, offsetRight = 0.2, white = 0) 		
:> bitmap(file = demo_saveImg)
;