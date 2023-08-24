// export R# source type define for javascript/typescript language
//
// package_source=voyager1

declare namespace voyager1 {
   module _ {
      /**
      */
      function onload(): object;
   }
   data_chunks: any;
   /**
     * @param offsetLeft default value Is ``0.15``.
     * @param offsetRight default value Is ``0.1``.
     * @param white default value Is ``0.8``.
     * @param luminous default value Is ``false``.
   */
   function decodeImage(start: object, wavFile: string, offsetLeft?: any, offsetRight?: any, white?: any, luminous?: any): object;
   /**
   */
   function export_from_chunks(wavFile: string, output_dir: string): object;
}
