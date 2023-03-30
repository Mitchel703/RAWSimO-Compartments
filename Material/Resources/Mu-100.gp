reset
# Output definition
set terminal pdfcairo enhanced size 7, 3 font "Consolas, 12"
set lmargin 13
set rmargin 13
# Parameters
set key right top Right
set grid
set style fill solid 0.75
# Line-Styles
set style line 1 linetype 1 linecolor rgb "#7090c8" linewidth 1
set output "Mu-100.pdf"
set title "Mu-100"
set xlabel "Frequency"
set ylabel "SKU count"
plot \
"Mu-100groups.dat" u 1:2 w boxes linestyle 1 t "SKU frequencies"
set title "Mu-100"
set xlabel "SKU"
set ylabel "frequency"
plot \
"Mu-100simple.dat" u 1:2 w steps linestyle 1 t "SKU frequencies"
set title "Mu-100"
set xlabel "SKU"
set ylabel "probability"
plot \
"Mu-100probability.dat" u 1:2 w steps linestyle 1 t "SKU probabilities"
set title "Mu-100"
set xlabel "SKU"
set ylabel "size"
plot \
"Mu-100weights.dat" u 1:2 w steps linestyle 1 t "SKU size"
set title "Mu-100"
set xlabel "SKU"
set ylabel "units"
plot \
"Mu-100bundlesizes.dat" u 1:2 w steps linestyle 1 t "SKU replenishment order size"
reset
exit
