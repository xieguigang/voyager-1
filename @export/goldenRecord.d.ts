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
       * render the decoded pixels as a gray scale or a pseudo color bitmap image.
       *  
       *  the intensity data is normalized by the percentile based auto levels, and the
       *  polarity is inverted by default because the image data of the golden record is
       *  stored as a photographic negative.
       * 
       * 
        * @param pixels -
        * @param luminous render as a pseudo color image
        * 
        * + default value Is ``false``.
        * @param white the white point gain, 0 or 1 means no adjustment
        * 
        * + default value Is ``1``.
        * @param gamma the gamma correction, 1 means no correction
        * 
        * + default value Is ``1``.
        * @param invert the black/white polarity
        * 
        * + default value Is ``true``.
        * @param percentile the black point and the white point percentiles, like ``c(0.005, 0.995)``
        * 
        * + default value Is ``null``.
        * @param palette the palette name of the pseudo color rendering
        * 
        * + default value Is ``'gray'``.
        * @param smooth the radius of the across scan line smoothing, 0 for disable
        * 
        * + default value Is ``0``.
        * @param height the output image height in pixels, 0 for keep the original size
        * 
        * + default value Is ``0``.
      */
      function bitmap(pixels: object, luminous?: boolean, white?: number, gamma?: number, invert?: boolean, percentile?: number, palette?: string, smooth?: object, height?: object): object;
   }
   /**
    * measure the data chunk size of the wav data for current image chunk
    * 
    * 
     * @param wav -
     * @param chunk -
   */
   function chunk_size(wav: object, chunk: object): object;
   module decode {
      /**
       * the empirical analysis of an image data section: the scan line period, the
       *  pixel clock, the brightness encoding scheme, the left/right channel
       *  relationship and the color component detection result.
       * 
       * 
        * @param wav -
        * @param chunk -
        * @param env_window the low pass window of the signal envelope
        * 
        * + default value Is ``96``.
        * @param env -
        * 
        * + default value Is ``null``.
      */
      function report(wav: object, chunk: object, env_window?: object, env?: object): object;
   }
   /**
    * write the image decode intensity data as csv file
    * 
    * 
     * @param decode -
     * @param file -
   */
   function write_csv(decode: object, file: string): any;
}
