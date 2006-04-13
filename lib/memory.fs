\ Copy at most 256 bytes of memory


code memcpy ( src dst n -- )
    \ Save FSR2 in temp_x2
    intr-protect
    FSR2L temp_x2 movff
    FSR2H temp_x2 1 + movff
    \ Destination goes into FSR2
    POSTDEC0 FSR2H movff
    POSTDEC0 FSR2L movff
    \ Source goes into FSR1
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    \ Copy bytes
label memcpy_loop
    POSTINC1 POSTINC2 movff
    WREG ,f ,a decfsz
    memcpy_loop bra
    \ Restore FSR2
    temp_x2 FSR2L movff
    temp_x2 1 + FSR2H movff
    intr-unprotect
    return
;code inw

