// export R# package module type define for javascript/typescript language
//
//    imports "goldenRecord" from "voyager-1";
//
// ref=voyager_1.Rscript@voyager-1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null

/**
 * 
*/
declare namespace goldenRecord {
   module as {
      /**
        * @param luminous default value Is ``false``.
      */
      function bitmap(pixels: object, luminous?: boolean): object;
   }
   /**
    * measure the data chunk size of the wav data for current image chunk
    * 
    * 
     * @param wav -
     * @param chunk -
   */
   function chunk_size(wav: object, chunk: object): object;
   /**
    * Decode the wav data as the pixel scans
    * 
    * 
     * @param wav -
     * @param chunk -
     * @param decode -
     * @param size -
     * 
     * + default value Is ``364``.
     * @param offsetLeft 
     * + default value Is ``0.2``.
     * @param offsetRight 
     * + default value Is ``0.2``.
     * @param env -
     * 
     * + default value Is ``null``.
   */
   function decode(wav: object, chunk: object, decode: object, size?: object, offsetLeft?: number, offsetRight?: number, env?: object): object;
}
