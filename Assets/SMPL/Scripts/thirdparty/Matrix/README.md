# LightweightMatrixC\# 

## Preface
Lightweight fast matrix class in C# based on [http://blog.ivank.net/](http://blog.ivank.net/lightweight-matrix-class-in-c-strassen-algorithm-lu-decomposition.html).  
I also did some changes based on the comments on the original page.

## Documentation
All this is written in C#. `Parse()` and `ToString()` methods included. Matrix class can throw exceptions (MException).

### Implemented matrix operations

- exponentiation by integer
- LU decomposition
- determinant
- inversion
- solve system of linear equations
- transpose, ...

### Overridden infix operators

- array-like access (`a[2,4]`)
- addition (`a + b`)
- subtraction (`a - b`)
- unary minus (`-a`)
- multiplication (`a * b`)
- constant multiplication (`5.4 * a`)

### Strassen algorithm (fast multiplication of large matrices)

It contains matrix multiplication with Strassen algorithm, which is memory efficient and cahce-oblivious, it works with still the same array in each level of recursion.

This algorithm is automatically selected if matrices are of form `2^N x 2^N, N>5`.
