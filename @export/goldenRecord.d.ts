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
        * the intensity data is normalized by the percentile based auto levels, and the
        * polarity is inverted by default because the image data of the golden record is
        * stored as a photographic negative.
        *
        * @param pixels the decoded image pixels
        * @param luminous render as a pseudo color image
        * @param white the white point gain, 0 or 1 means no adjustment
        * @param gamma the gamma correction, 1 means no correction
        * @param invert the black/white polarity
        * @param percentile the black point and the white point percentiles, like ``c(0.005, 0.995)``
        * @param palette the palette name of the pseudo color rendering
        * @param smooth the radius of the across scan line smoothing, 0 for disable
        * @param height the output image height in pixels, 0 for keep the original size
        *
        * + default value Is ``false``.
        * + default value Is ``1``.
        * + default value Is ``1``.
        * + default value Is ``true``.
        * + default value Is ``null``.
        * + default value Is ``"gray"``.
        * + default value Is ``0``.
        * + default value Is ``0``.
      */
      function bitmap(pixels: object, luminous?: boolean, white?: number, gamma?: number, invert?: boolean, percentile?: object, palette?: string, smooth?: number, height?: number): object;
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
    * the empirical analysis of an image data section: the scan line period, the
    * pixel clock, the brightness encoding scheme, the left/right channel
    * relationship and the color component detection result.
    *
    * @param wav -
    * @param chunk -
    * @param env_window the low pass window of the signal envelope
    * @param env -
    *
    * + default value Is ``96``.
    * + default value Is ``null``.
   */
   function decode.report(wav: object, chunk: object, env_window?: number, env?: object): object;
   /**
    * Decode the wav data as the image pixels.
    *
    * the scan line period and the pixel clock are measured from the waveform data
    * itself (see the ``decode.report`` function for the empirical analysis), the
    * ``offsetLeft``/``offsetRight`` parameters of the original implementation are
    * kept only for the backward compatibility of the existing scripts.
    *
    * @param wav -
    * @param chunk -
    * @param decode the decoder arguments, the invalid values are filled by the measured ones
    * @param size the pixel cells (the image rows) per scan line, 0 for the default 364
    * @param offsetLeft -
    * @param offsetRight -
    * @param pixels the alias of the ``size`` parameter, the value of ``size`` wins when both are assigned
    * @param lines the number of the scan lines (the image columns), 0 for the default 512
    * @param align apply the per scan line synchronization
    * @param guard the samples which are skipped after the sync pulse position
    * @param env -
    *
    * + default value Is ``0``.
    * + default value Is ``0.2``.
    * + default value Is ``0.2``.
    * + default value Is ``0``.
    * + default value Is ``0``.
    * + default value Is ``true``.
    * + default value Is ``0``.
    * + default value Is ``null``.
   */
   function decode(wav: object, chunk: object, decode: object, size?: object, offsetLeft?: number, offsetRight?: number, pixels?: object, lines?: object, align?: boolean, guard?: number, env?: object): object;
}
