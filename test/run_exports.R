require(voyager1);

# raw wav data
const goldenRecord as string = "J:/GoogleDrive/Voyager/384kHzStereo.wav";
const demo_saveImg as string = `${!script$dir}/../../docs/images/`;

export_from_chunks(
    wavFile    = goldenRecord, 
    output_dir = demo_saveImg
);