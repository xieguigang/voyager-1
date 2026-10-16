// export R# source type define for javascript/typescript language
//
// package_source=voyager1

declare namespace voyager1 {
   module _ {
      /**
        * @param size default value Is ``&image_pixel_size``.
        * @param lines default value Is ``&image_scan_lines``.
        * @param smooth default value Is ``0``.
        * @param report default value Is ``false``.
      */
      function export_chunk(wav: any, chunk_name: any, size?: any, lines?: any, smooth?: any, report?: any): object;
      /**
      */
      function onload(): object;
   }
   data_chunks: any;
   /**
     * @param size default value Is ``364``.
     * @param lines default value Is ``512``.
     * @param white default value Is ``1``.
     * @param gamma default value Is ``1``.
     * @param percentile default value Is ``null``.
     * @param palette default value Is ``gray``.
     * @param smooth default value Is ``0``.
     * @param luminous default value Is ``false``.
     * @param report default value Is ``false``.
   */
   function decodeImage(start: object, wavFile: string, size?: any, lines?: any, white?: any, gamma?: any, percentile?: any, palette?: any, smooth?: any, luminous?: any, report?: any): object;
   /**
     * @param size default value Is ``&image_pixel_size``.
     * @param lines default value Is ``&image_scan_lines``.
     * @param smooth default value Is ``0``.
     * @param report default value Is ``false``.
   */
   function export_from_chunks(wavFile: string, output_dir: string, size?: any, lines?: any, smooth?: any, report?: any): object;
   image_pixel_size: any;
   image_scan_lines: any;
   image_section_length: any;
}
