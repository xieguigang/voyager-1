// export R# source type define for javascript/typescript language
//
// package_source=voyager1

declare namespace voyager1 {
   module _ {
      /**
      */
      function export_chunk(wav: any, chunk_name: any, size?: any, lines?: any, smooth?: any, report?: any): object;
      /**
      */
      function onload(): object;
   }
   data_chunks: any;
   /** the frame length of an image data section */
   image_section_length: any;
   /** the pixel cells (the image rows) inside a single scan line */
   image_pixel_size: any;
   /** the number of the scan lines (the image columns) of a single image */
   image_scan_lines: any;
   /**
     * @param size the pixel cells (the image rows) inside a single scan line, default 364
     * @param lines the number of the scan lines (the image columns), default 512
     * @param white the white point gain of the rendering, 1 means no adjustment
     * @param gamma the gamma correction of the rendering, 1 means no correction
     * @param percentile the black point and the white point percentiles of the auto levels
     * @param palette the palette name of the pseudo color rendering
     * @param smooth the radius of the across scan line smoothing, 0 for disable
     * @param luminous render the image as the pseudo color image
     * @param report print the empirical analysis report of the data section
   */
   function decodeImage(start: object, wavFile: string, size?: any, lines?: any, white?: any, gamma?: any, percentile?: any, palette?: any, smooth?: any, luminous?: any, report?: any): object;
   /**
     * @param size the pixel cells (the image rows) inside a single scan line
     * @param lines the number of the scan lines (the image columns)
     * @param smooth the radius of the across scan line smoothing
     * @param report print the empirical analysis report of every image section
   */
   function export_from_chunks(wavFile: string, output_dir: string, size?: any, lines?: any, smooth?: any, report?: any): object;
}
